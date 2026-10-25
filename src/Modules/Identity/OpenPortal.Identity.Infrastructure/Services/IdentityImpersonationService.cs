using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Application.Auditing;
using OpenPortal.Identity.Application.Notifications;
using OpenPortal.Identity.Domain.Users;
using OpenPortal.SharedKernel.Auditing;
using OpenPortal.SharedKernel.Notifications;
using OpenPortal.SharedKernel.Results;
using OpenPortal.SharedKernel.Time;

namespace OpenPortal.Identity.Infrastructure.Services;

/// <inheritdoc />
internal sealed class IdentityImpersonationService : IImpersonationService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserLookup _userLookup;
    private readonly AdministrationGuard _guard;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IAuditTrail _audit;
    private readonly INotifier _notifier;

    public IdentityImpersonationService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        UserLookup userLookup,
        AdministrationGuard guard,
        ICurrentUser currentUser,
        IClock clock,
        IAuditTrail audit,
        INotifier notifier)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _userLookup = userLookup;
        _guard = guard;
        _currentUser = currentUser;
        _clock = clock;
        _audit = audit;
        _notifier = notifier;
    }

    public async Task<Result> StartAsync(Guid userId, StartImpersonationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Never delegated through pages, and never from inside another support session.
        if (!_guard.CallerIsAdministrator || _currentUser.ImpersonatorId is not null || _currentUser.UserId is not Guid adminId)
        {
            return Result.Failure(UserErrors.Forbidden);
        }

        var reason = request.Reason?.Trim();
        if (string.IsNullOrEmpty(reason) || reason.Length > StartImpersonationRequest.ReasonMaxLength)
        {
            return Result.Failure(UserErrors.ReasonRequired);
        }

        var lookup = await _userLookup.FindAsync(userId, cancellationToken).ConfigureAwait(false);
        if (lookup.IsFailure)
        {
            return Result.Failure(lookup.Error);
        }

        var target = lookup.Value;
        var now = _clock.UtcNow;

        if (target.Id == adminId
            || target.PendingApproval
            || target.DeletionScheduledAtUtc is not null
            || (target.LockoutEnd is { } lockout && lockout > now)
            || await _userManager.IsInRoleAsync(target, Roles.Administrator).ConfigureAwait(false))
        {
            return Result.Failure(UserErrors.ImpersonationRefused);
        }

        var adminEmail = _currentUser.Email;

        await _signInManager.SignInWithClaimsAsync(
                target,
                isPersistent: false,
                [
                    new Claim(PortalClaimTypes.ImpersonatorId, adminId.ToString()),
                    new Claim(PortalClaimTypes.ImpersonatorEmail, adminEmail ?? string.Empty),
                    new Claim(PortalClaimTypes.ImpersonationStarted, now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
                ])
            .ConfigureAwait(false);

        // The sign-in already switched the request to the support session: the administrator is the actor, acting
        // on behalf of the account.
        await _audit.RecordAsync(
                AuditEvent.Succeeded(
                    IdentityAuditActions.ImpersonationStarted,
                    target.ToAuditSubject(),
                    new Dictionary<string, string?> { ["reason"] = reason }),
                cancellationToken)
            .ConfigureAwait(false);

        await _notifier.NotifyAsync(
                new Notice
                {
                    Type = IdentityNotificationTypes.Impersonated,
                    Audience = NoticeAudience.Users(target.Id),
                    Parameters = new Dictionary<string, string?> { ["admin"] = adminEmail, ["reason"] = reason },
                    Link = "/account?tab=security",

                    // The sign-in above already made the account the current caller, who is normally left out.
                    IncludeCaller = true,
                    Security = true,
                },
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success();
    }

    public async Task<Result> StopAsync(CancellationToken cancellationToken)
    {
        if (_currentUser.ImpersonatorId is not Guid adminId)
        {
            return Result.Failure(UserErrors.NotImpersonating);
        }

        var target = _currentUser.UserId is Guid userId
            ? new AuditSubject(AuditSubjectTypes.User, userId.ToString(), _currentUser.Email)
            : null;
        var actor = new AuditSubject(AuditSubjectTypes.User, adminId.ToString(), _currentUser.ImpersonatorEmail);

        var lookup = await _userLookup.FindAsync(adminId, cancellationToken).ConfigureAwait(false);
        var admin = lookup.IsSuccess ? lookup.Value : null;

        // Back to the administrator only while they still are one and may sign in; otherwise simply signed out.
        if (admin is not null
            && await _userManager.IsInRoleAsync(admin, Roles.Administrator).ConfigureAwait(false)
            && await _signInManager.CanSignInAsync(admin).ConfigureAwait(false)
            && !await _userManager.IsLockedOutAsync(admin).ConfigureAwait(false))
        {
            await _signInManager.SignInAsync(admin, isPersistent: false).ConfigureAwait(false);
        }
        else
        {
            await _signInManager.SignOutAsync().ConfigureAwait(false);
        }

        await _audit.RecordAsync(
                new AuditEvent { Action = IdentityAuditActions.ImpersonationEnded, Actor = actor, Target = target },
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success();
    }
}
