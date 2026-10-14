using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Application.Auditing;
using OpenPortal.Identity.Application.Contracts;
using OpenPortal.Identity.Application.Notifications;
using OpenPortal.Identity.Domain.Invitations;
using OpenPortal.Identity.Domain.Users;
using OpenPortal.Identity.Infrastructure.Persistence;
using OpenPortal.SharedKernel.Auditing;
using OpenPortal.SharedKernel.Notifications;
using OpenPortal.SharedKernel.Results;
using OpenPortal.SharedKernel.Time;

namespace OpenPortal.Identity.Infrastructure.Services;

/// <inheritdoc />
internal sealed class IdentityInvitationService : IInvitationService
{
    private readonly IdentityDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly AdministrationGuard _guard;
    private readonly ICurrentUser _currentUser;
    private readonly RegistrationSettingsStore _settings;
    private readonly IAccountMessaging _messaging;
    private readonly IClock _clock;
    private readonly IAuditTrail _audit;
    private readonly INotifier _notifier;

    public IdentityInvitationService(
        IdentityDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        AdministrationGuard guard,
        ICurrentUser currentUser,
        RegistrationSettingsStore settings,
        IAccountMessaging messaging,
        IClock clock,
        IAuditTrail audit,
        INotifier notifier)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _signInManager = signInManager;
        _guard = guard;
        _currentUser = currentUser;
        _settings = settings;
        _messaging = messaging;
        _clock = clock;
        _audit = audit;
        _notifier = notifier;
    }

    public async Task<Result<PagedResult<InvitationDto>>> ListAsync(InvitationQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var guard = await _guard.EnsureCanAsync(UserAdministrationOperation.ManageUsers, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<PagedResult<InvitationDto>>.Failure(guard.Error);
        }

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, InvitationQuery.MaxPageSize);
        var now = _clock.UtcNow;

        var invitations = _dbContext.Invitations.AsNoTracking();

        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            var term = search.ToUpperInvariant();
            invitations = invitations.Where(invitation => invitation.NormalizedEmail.Contains(term));
        }

        // Expiry is computed from the clock, so each status is a condition on the dates.
        invitations = query.Status?.Trim().ToLowerInvariant() switch
        {
            InvitationStatuses.Pending => invitations.Where(invitation =>
                invitation.RevokedAtUtc == null && invitation.AcceptedAtUtc == null && invitation.ExpiresAtUtc > now),
            InvitationStatuses.Expired => invitations.Where(invitation =>
                invitation.RevokedAtUtc == null && invitation.AcceptedAtUtc == null && invitation.ExpiresAtUtc <= now),
            InvitationStatuses.Accepted => invitations.Where(invitation => invitation.AcceptedAtUtc != null),
            InvitationStatuses.Revoked => invitations.Where(invitation => invitation.RevokedAtUtc != null),
            _ => invitations,
        };

        var totalCount = await invitations.CountAsync(cancellationToken).ConfigureAwait(false);

        var items = await invitations
            .OrderByDescending(invitation => invitation.CreatedAtUtc)
            .ThenBy(invitation => invitation.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var dtos = await ToDtosAsync(items, cancellationToken).ConfigureAwait(false);

        return Result<PagedResult<InvitationDto>>.Success(new PagedResult<InvitationDto>(dtos, page, pageSize, totalCount));
    }

    public async Task<Result<InvitationIssuedDto>> CreateAsync(CreateInvitationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var guard = await _guard.EnsureCanAsync(UserAdministrationOperation.ManageUsers, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<InvitationIssuedDto>.Failure(guard.Error);
        }

        if (_currentUser.UserId is not Guid inviterId)
        {
            return Result<InvitationIssuedDto>.Failure(UserErrors.NotAuthenticated);
        }

        var email = request.Email.Trim();

        if (await _userManager.FindByEmailAsync(email).ConfigureAwait(false) is not null)
        {
            return Result<InvitationIssuedDto>.Failure(UserErrors.DuplicateEmail);
        }

        var now = _clock.UtcNow;
        var normalized = email.ToUpperInvariant();

        var waiting = await _dbContext.Invitations
            .AnyAsync(
                invitation => invitation.NormalizedEmail == normalized
                    && invitation.RevokedAtUtc == null
                    && invitation.AcceptedAtUtc == null
                    && invitation.ExpiresAtUtc > now,
                cancellationToken)
            .ConfigureAwait(false);

        if (waiting)
        {
            return Result<InvitationIssuedDto>.Failure(UserErrors.InvitationAlreadyPending);
        }

        var settings = await _settings.ReadAsync(cancellationToken).ConfigureAwait(false);
        var token = NewToken();

        var created = UserInvitation.Create(
            Guid.NewGuid(),
            email,
            request.FirstName,
            request.LastName,
            request.Message,
            Hash(token),
            inviterId,
            request.GroupIds,
            request.OrganizationIds,
            now,
            TimeSpan.FromDays(settings.InvitationLifetimeDays));

        if (created.IsFailure)
        {
            return Result<InvitationIssuedDto>.Failure(created.Error);
        }

        var invitation = created.Value;
        _dbContext.Invitations.Add(invitation);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var sent = await SendAsync(invitation, token, cancellationToken).ConfigureAwait(false);

        await _audit.RecordAsync(
                AuditEvent.Succeeded(
                    IdentityAuditActions.Invited,
                    invitation.ToAuditSubject(),
                    new Dictionary<string, string?>
                    {
                        ["groups"] = invitation.GroupIds.Count.ToString(CultureInfo.InvariantCulture),
                        ["organizations"] = invitation.OrganizationIds.Count.ToString(CultureInfo.InvariantCulture),
                        ["emailed"] = sent ? "true" : "false",
                    }),
                cancellationToken)
            .ConfigureAwait(false);

        return Result<InvitationIssuedDto>.Success(await IssuedAsync(invitation, token, sent, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<InvitationIssuedDto>> ResendAsync(Guid invitationId, CancellationToken cancellationToken)
    {
        var guard = await _guard.EnsureCanAsync(UserAdministrationOperation.ManageUsers, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<InvitationIssuedDto>.Failure(guard.Error);
        }

        var invitation = await _dbContext.Invitations
            .SingleOrDefaultAsync(candidate => candidate.Id == invitationId, cancellationToken)
            .ConfigureAwait(false);

        if (invitation is null)
        {
            return Result<InvitationIssuedDto>.Failure(UserErrors.InvitationNotFound);
        }

        // The address may have got an account some other way since.
        if (await _userManager.FindByEmailAsync(invitation.Email).ConfigureAwait(false) is not null)
        {
            return Result<InvitationIssuedDto>.Failure(UserErrors.DuplicateEmail);
        }

        var settings = await _settings.ReadAsync(cancellationToken).ConfigureAwait(false);
        var token = NewToken();

        var renewed = invitation.Renew(Hash(token), _clock.UtcNow, TimeSpan.FromDays(settings.InvitationLifetimeDays));
        if (renewed.IsFailure)
        {
            return Result<InvitationIssuedDto>.Failure(renewed.Error);
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var sent = await SendAsync(invitation, token, cancellationToken).ConfigureAwait(false);

        await _audit.RecordAsync(
                AuditEvent.Succeeded(
                    IdentityAuditActions.InvitationResent,
                    invitation.ToAuditSubject(),
                    new Dictionary<string, string?> { ["emailed"] = sent ? "true" : "false" }),
                cancellationToken)
            .ConfigureAwait(false);

        return Result<InvitationIssuedDto>.Success(await IssuedAsync(invitation, token, sent, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result> RevokeAsync(Guid invitationId, CancellationToken cancellationToken)
    {
        var guard = await _guard.EnsureCanAsync(UserAdministrationOperation.ManageUsers, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return guard;
        }

        var invitation = await _dbContext.Invitations
            .SingleOrDefaultAsync(candidate => candidate.Id == invitationId, cancellationToken)
            .ConfigureAwait(false);

        if (invitation is null)
        {
            return Result.Failure(UserErrors.InvitationNotFound);
        }

        var revoked = invitation.Revoke(_clock.UtcNow);
        if (revoked.IsFailure)
        {
            return revoked;
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _audit.RecordAsync(AuditEvent.Succeeded(IdentityAuditActions.InvitationRevoked, invitation.ToAuditSubject()), cancellationToken)
            .ConfigureAwait(false);

        return Result.Success();
    }

    public async Task<Result<InvitationPreviewDto>> PreviewAsync(string token, CancellationToken cancellationToken)
    {
        var found = await FindOpenAsync(token, cancellationToken).ConfigureAwait(false);
        if (found.IsFailure)
        {
            return Result<InvitationPreviewDto>.Failure(found.Error);
        }

        var invitation = found.Value;
        var inviter = await _userManager.FindByIdAsync(invitation.InvitedByUserId.ToString()).ConfigureAwait(false);

        return Result<InvitationPreviewDto>.Success(new InvitationPreviewDto(
            invitation.Email,
            invitation.FirstName,
            invitation.LastName,
            inviter?.DisplayName,
            invitation.Message,
            invitation.ExpiresAtUtc));
    }

    public async Task<Result<AcceptedInvitationDto>> AcceptAsync(AcceptInvitationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var found = await FindOpenAsync(request.Token, cancellationToken).ConfigureAwait(false);
        if (found.IsFailure)
        {
            return Result<AcceptedInvitationDto>.Failure(found.Error);
        }

        var invitation = found.Value;

        if (await _userManager.FindByEmailAsync(invitation.Email).ConfigureAwait(false) is not null)
        {
            return Result<AcceptedInvitationDto>.Failure(UserErrors.DuplicateEmail);
        }

        var now = _clock.UtcNow;
        var account = ApplicationUser.Create(
            Guid.NewGuid(),
            invitation.Email,
            new UserDetails(request.FirstName, request.LastName, null, null, null, null, null, null, null, null),
            now,
            AccountOrigin.Invited);

        if (account.IsFailure)
        {
            return Result<AcceptedInvitationDto>.Failure(account.Error);
        }

        var user = account.Value;

        // The link reached the address, which is what confirming it means.
        user.EmailConfirmed = true;

        var created = await _userManager.CreateAsync(user, request.Password).ConfigureAwait(false);
        if (!created.Succeeded)
        {
            return Result<AcceptedInvitationDto>.Failure(created.ToError(UserErrors.SaveFailed));
        }

        var role = await _userManager.AddToRoleAsync(user, Roles.User).ConfigureAwait(false);
        if (!role.Succeeded)
        {
            await _userManager.DeleteAsync(user).ConfigureAwait(false);
            return Result<AcceptedInvitationDto>.Failure(role.ToError(UserErrors.RoleAssignmentFailed));
        }

        var accepted = invitation.Accept(user.Id, now);
        if (accepted.IsFailure)
        {
            await _userManager.DeleteAsync(user).ConfigureAwait(false);
            return Result<AcceptedInvitationDto>.Failure(accepted.Error);
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        user.RecordSignIn(now);
        await _userManager.UpdateAsync(user).ConfigureAwait(false);
        await _signInManager.SignInAsync(user, isPersistent: false).ConfigureAwait(false);

        await _audit.RecordAsync(
                new AuditEvent
                {
                    Action = IdentityAuditActions.InvitationAccepted,
                    Actor = user.ToAuditSubject(),
                    Target = invitation.ToAuditSubject(),
                },
                cancellationToken)
            .ConfigureAwait(false);

        await _notifier.NotifyAsync(
                new Notice
                {
                    Type = IdentityNotificationTypes.InvitationAccepted,
                    Audience = NoticeAudience.Users(invitation.InvitedByUserId),
                    Parameters = new Dictionary<string, string?> { ["user"] = user.DisplayName, ["email"] = user.Email },
                    Link = "/admin/users",
                },
                cancellationToken)
            .ConfigureAwait(false);

        return Result<AcceptedInvitationDto>.Success(new AcceptedInvitationDto(
            user.Id,
            user.Email!,
            invitation.InvitedByUserId,
            invitation.GroupIds,
            invitation.OrganizationIds));
    }

    /// <summary>The invitation behind a token, while it can still be accepted.</summary>
    private async Task<Result<UserInvitation>> FindOpenAsync(string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Result<UserInvitation>.Failure(UserErrors.InvitationInvalid);
        }

        var hash = Hash(token.Trim());
        var invitation = await _dbContext.Invitations
            .SingleOrDefaultAsync(candidate => candidate.TokenHash == hash, cancellationToken)
            .ConfigureAwait(false);

        return invitation?.StatusAt(_clock.UtcNow) switch
        {
            InvitationStatus.Pending => Result<UserInvitation>.Success(invitation),
            InvitationStatus.Expired => Result<UserInvitation>.Failure(UserErrors.InvitationExpired),
            _ => Result<UserInvitation>.Failure(UserErrors.InvitationInvalid),
        };
    }

    private async Task<bool> SendAsync(UserInvitation invitation, string token, CancellationToken cancellationToken)
    {
        if (!_messaging.CanSendEmail)
        {
            return false;
        }

        var inviter = _currentUser.UserId is Guid inviterId
            ? await _userManager.FindByIdAsync(inviterId.ToString()).ConfigureAwait(false)
            : null;

        return await _messaging.SendAsync(
                new AccountMessage(
                    AccountMessageKinds.Invitation,
                    invitation.Email,
                    invitation.FirstName,
                    Language: null,
                    LinkPath(token),
                    new Dictionary<string, string?>
                    {
                        ["name"] = invitation.FirstName,
                        ["inviter"] = inviter?.DisplayName,
                        ["message"] = invitation.Message,
                        ["expires"] = invitation.ExpiresAtUtc.ToString("O", CultureInfo.InvariantCulture),
                    }),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<InvitationIssuedDto> IssuedAsync(UserInvitation invitation, string token, bool sent, CancellationToken cancellationToken)
    {
        var dto = (await ToDtosAsync([invitation], cancellationToken).ConfigureAwait(false))[0];

        return new InvitationIssuedDto(dto, _messaging.LinkTo(LinkPath(token)), sent);
    }

    private async Task<IReadOnlyList<InvitationDto>> ToDtosAsync(IReadOnlyList<UserInvitation> invitations, CancellationToken cancellationToken)
    {
        var inviterIds = invitations.Select(invitation => invitation.InvitedByUserId).Distinct().ToArray();
        var inviters = await _userManager.Users
            .AsNoTracking()
            .Where(user => inviterIds.Contains(user.Id))
            .Select(user => new { user.Id, user.DisplayName })
            .ToDictionaryAsync(user => user.Id, user => user.DisplayName, cancellationToken)
            .ConfigureAwait(false);

        var now = _clock.UtcNow;

        return invitations
            .Select(invitation => new InvitationDto(
                invitation.Id,
                invitation.Email,
                invitation.FirstName,
                invitation.LastName,
                invitation.Message,
                ToContract(invitation.StatusAt(now)),
                invitation.InvitedByUserId,
                inviters.GetValueOrDefault(invitation.InvitedByUserId),
                invitation.CreatedAtUtc,
                invitation.ExpiresAtUtc,
                invitation.LastSentAtUtc,
                invitation.AcceptedAtUtc,
                invitation.AcceptedUserId,
                invitation.GroupIds,
                invitation.OrganizationIds))
            .ToList();
    }

    private static string LinkPath(string token) => $"/invitation?token={Uri.EscapeDataString(token)}";

    private static string ToContract(InvitationStatus status) => status switch
    {
        InvitationStatus.Pending => InvitationStatuses.Pending,
        InvitationStatus.Accepted => InvitationStatuses.Accepted,
        InvitationStatus.Revoked => InvitationStatuses.Revoked,
        InvitationStatus.Expired => InvitationStatuses.Expired,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    /// <summary>256 random bits, URL-safe: what the link carries. Only its hash is stored.</summary>
    private static string NewToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

/// <summary>How invitations are named in the audit log: by id, labelled with the invited address.</summary>
internal static class InvitationAudit
{
    public static AuditSubject ToAuditSubject(this UserInvitation invitation) =>
        new(AuditSubjectTypes.Invitation, invitation.Id.ToString(), invitation.Email);
}
