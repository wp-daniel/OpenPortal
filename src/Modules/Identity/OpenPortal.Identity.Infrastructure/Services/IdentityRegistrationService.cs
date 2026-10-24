using Microsoft.AspNetCore.Identity;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Application.Auditing;
using OpenPortal.Identity.Application.Contracts;
using OpenPortal.Identity.Application.Notifications;
using OpenPortal.Identity.Domain.Settings;
using OpenPortal.Identity.Domain.Users;
using OpenPortal.SharedKernel.Auditing;
using OpenPortal.SharedKernel.Notifications;
using OpenPortal.SharedKernel.Results;
using OpenPortal.SharedKernel.Time;

namespace OpenPortal.Identity.Infrastructure.Services;

/// <inheritdoc />
internal sealed class IdentityRegistrationService : IRegistrationService
{
    private readonly IdentityLegalDocumentService _legal;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly RegistrationSettingsStore _settings;
    private readonly IAccountMessaging _messaging;
    private readonly IClock _clock;
    private readonly IAuditTrail _audit;
    private readonly INotifier _notifier;

    public IdentityRegistrationService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        RegistrationSettingsStore settings,
        IAccountMessaging messaging,
        IClock clock,
        IAuditTrail audit,
        INotifier notifier,
        IdentityLegalDocumentService legal)
    {
        _legal = legal;
        _userManager = userManager;
        _signInManager = signInManager;
        _settings = settings;
        _messaging = messaging;
        _clock = clock;
        _audit = audit;
        _notifier = notifier;
    }

    public async Task<RegistrationInfoDto> GetInfoAsync(CancellationToken cancellationToken)
    {
        var settings = await _settings.ReadAsync(cancellationToken).ConfigureAwait(false);

        return new RegistrationInfoDto(settings.IsOpen, settings.Mode == RegistrationMode.Approval);
    }

    public async Task<Result<RegistrationResultDto>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var settings = await _settings.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (!settings.IsOpen)
        {
            return Result<RegistrationResultDto>.Failure(UserErrors.RegistrationClosed);
        }

        var email = request.Email.Trim();

        if (!settings.AllowsEmail(email))
        {
            await RecordRefusalAsync(email, "domain_not_allowed", cancellationToken).ConfigureAwait(false);
            return Result<RegistrationResultDto>.Failure(UserErrors.RegistrationDomainNotAllowed);
        }

        var confirm = settings.RequireEmailConfirmation;
        if (confirm && !await _messaging.CanSendEmailAsync(cancellationToken).ConfigureAwait(false))
        {
            return Result<RegistrationResultDto>.Failure(UserErrors.EmailDeliveryUnavailable);
        }

        var legal = await _legal.RequireAcceptedAsync(request.AcceptedLegalDocumentIds, cancellationToken).ConfigureAwait(false);
        if (legal.IsFailure)
        {
            return Result<RegistrationResultDto>.Failure(legal.Error);
        }

        if (await _userManager.FindByEmailAsync(email).ConfigureAwait(false) is not null)
        {
            await RecordRefusalAsync(email, "email_in_use", cancellationToken).ConfigureAwait(false);

            // With confirmation on, the answer is the one a new address gets: whoever owns the address learns
            // nothing new from a mailbox they already control, and nobody else learns who has an account.
            return confirm
                ? Result<RegistrationResultDto>.Success(new RegistrationResultDto(RegistrationOutcomes.ConfirmationSent))
                : Result<RegistrationResultDto>.Failure(UserErrors.DuplicateEmail);
        }

        var account = ApplicationUser.Create(
            Guid.NewGuid(),
            email,
            new UserDetails(request.FirstName, request.LastName, null, null, null, null, null, null, null, null),
            _clock.UtcNow,
            AccountOrigin.Registered);

        if (account.IsFailure)
        {
            return Result<RegistrationResultDto>.Failure(account.Error);
        }

        var user = account.Value;
        var needsApproval = settings.Mode == RegistrationMode.Approval;
        if (needsApproval)
        {
            user.RequireApproval();
        }

        var created = await _userManager.CreateAsync(user, request.Password).ConfigureAwait(false);
        if (!created.Succeeded)
        {
            return Result<RegistrationResultDto>.Failure(created.ToError(UserErrors.SaveFailed));
        }

        var role = await _userManager.AddToRoleAsync(user, Roles.User).ConfigureAwait(false);
        if (!role.Succeeded)
        {
            await _userManager.DeleteAsync(user).ConfigureAwait(false);
            return Result<RegistrationResultDto>.Failure(role.ToError(UserErrors.RoleAssignmentFailed));
        }

        await _legal.RecordAsync(user.Id, legal.Value, cancellationToken).ConfigureAwait(false);

        await _audit.RecordAsync(
                new AuditEvent
                {
                    Action = IdentityAuditActions.Registered,
                    Actor = user.ToAuditSubject(),
                    Target = user.ToAuditSubject(),
                    Details = new Dictionary<string, string?>
                    {
                        ["approval"] = needsApproval ? "required" : "none",
                        ["emailConfirmation"] = confirm ? "required" : "none",
                    },
                },
                cancellationToken)
            .ConfigureAwait(false);

        await _notifier.NotifyAsync(
                new Notice
                {
                    Type = needsApproval ? IdentityNotificationTypes.RegistrationPending : IdentityNotificationTypes.UserRegistered,
                    Audience = NoticeAudience.Holders(IdentityNotificationRoles.UserReviewers),
                    Parameters = new Dictionary<string, string?> { ["user"] = user.DisplayName, ["email"] = user.Email },
                    Link = needsApproval ? "/admin/users?status=pending" : "/admin/users",
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (confirm)
        {
            await SendConfirmationAsync(user, cancellationToken).ConfigureAwait(false);
            return Result<RegistrationResultDto>.Success(new RegistrationResultDto(RegistrationOutcomes.ConfirmationSent));
        }

        if (needsApproval)
        {
            return Result<RegistrationResultDto>.Success(new RegistrationResultDto(RegistrationOutcomes.ApprovalPending));
        }

        user.RecordSignIn(_clock.UtcNow);
        await _userManager.UpdateAsync(user).ConfigureAwait(false);
        await _signInManager.SignInAsync(user, isPersistent: false).ConfigureAwait(false);

        return Result<RegistrationResultDto>.Success(new RegistrationResultDto(RegistrationOutcomes.SignedIn));
    }

    public async Task<Result<RegistrationResultDto>> ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await _userManager.FindByIdAsync(request.UserId.ToString()).ConfigureAwait(false);
        if (user is null)
        {
            return Result<RegistrationResultDto>.Failure(UserErrors.EmailConfirmationInvalid);
        }

        if (!user.EmailConfirmed)
        {
            var confirmed = await _userManager.ConfirmEmailAsync(user, request.Token).ConfigureAwait(false);
            if (!confirmed.Succeeded)
            {
                return Result<RegistrationResultDto>.Failure(UserErrors.EmailConfirmationInvalid);
            }

            await _audit.RecordAsync(
                    new AuditEvent { Action = IdentityAuditActions.EmailConfirmed, Actor = user.ToAuditSubject() },
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return Result<RegistrationResultDto>.Success(new RegistrationResultDto(
            user.PendingApproval ? RegistrationOutcomes.ApprovalPending : RegistrationOutcomes.Confirmed));
    }

    public async Task ResendConfirmationAsync(ResendConfirmationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await _userManager.FindByEmailAsync(request.Email.Trim()).ConfigureAwait(false);

        if (user is not null && await _settings.AwaitsEmailConfirmationAsync(user, cancellationToken).ConfigureAwait(false))
        {
            await SendConfirmationAsync(user, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SendConfirmationAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user).ConfigureAwait(false);
        var link = $"/confirm-email?user={user.Id}&token={Uri.EscapeDataString(token)}";

        await _messaging.SendAsync(
                new AccountMessage(
                    AccountMessageKinds.EmailConfirmation,
                    user.Email!,
                    user.DisplayName,
                    user.Language,
                    link,
                    new Dictionary<string, string?> { ["name"] = user.FirstName }),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private Task RecordRefusalAsync(string email, string reason, CancellationToken cancellationToken) =>
        _audit.RecordAsync(
            new AuditEvent
            {
                Action = IdentityAuditActions.Registered,
                Outcome = AuditOutcome.Failure,
                Actor = new AuditSubject(AuditSubjectTypes.User, string.Empty, email),
                Details = new Dictionary<string, string?> { ["reason"] = reason },
            },
            cancellationToken);
}
