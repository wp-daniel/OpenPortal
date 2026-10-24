using System.Globalization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Application.Auditing;
using OpenPortal.Identity.Application.Contracts;
using OpenPortal.Identity.Domain.Users;
using OpenPortal.SharedKernel.Auditing;
using OpenPortal.SharedKernel.Results;
using OpenPortal.SharedKernel.Time;

namespace OpenPortal.Identity.Infrastructure.Services;

/// <summary>The lifetime of a password reset link, shorter than the other emailed tokens (a day).</summary>
public sealed class PasswordResetTokenProviderOptions : DataProtectionTokenProviderOptions
{
    public const string ProviderName = "PortalPasswordReset";

    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);

    public PasswordResetTokenProviderOptions()
    {
        Name = ProviderName;
        TokenLifespan = Lifetime;
    }
}

/// <summary>
/// Issues password reset tokens. Like Identity's default provider, a token embeds the account's security stamp, so
/// it stops working as soon as the password changes: each link works once.
/// </summary>
internal sealed class PasswordResetTokenProvider : DataProtectorTokenProvider<ApplicationUser>
{
    public PasswordResetTokenProvider(
        IDataProtectionProvider dataProtectionProvider,
        IOptions<PasswordResetTokenProviderOptions> options,
        ILogger<DataProtectorTokenProvider<ApplicationUser>> logger)
        : base(dataProtectionProvider, options, logger)
    {
    }
}

/// <inheritdoc />
internal sealed class IdentityPasswordRecoveryService : IPasswordRecoveryService
{
    /// <summary>A lockout that lasts longer than this was set by an administrator: no reset link for such an account.</summary>
    private static readonly TimeSpan AdministrativeLockout = TimeSpan.FromDays(1);

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAccountMessaging _messaging;
    private readonly PersonalAccessTokenStore _tokens;
    private readonly IClock _clock;
    private readonly IAuditTrail _audit;

    public IdentityPasswordRecoveryService(
        UserManager<ApplicationUser> userManager,
        IAccountMessaging messaging,
        PersonalAccessTokenStore tokens,
        IClock clock,
        IAuditTrail audit)
    {
        _userManager = userManager;
        _messaging = messaging;
        _tokens = tokens;
        _clock = clock;
        _audit = audit;
    }

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken) => _messaging.CanSendEmailAsync(cancellationToken);

    public async Task RequestAsync(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var email = request.Email.Trim();
        var user = await _userManager.FindByEmailAsync(email).ConfigureAwait(false);
        var reason = user is null ? "unknown_address" : RefusalReason(user);

        if (reason is null && await _messaging.CanSendEmailAsync(cancellationToken).ConfigureAwait(false))
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user!).ConfigureAwait(false);

            await _messaging.SendAsync(
                    new AccountMessage(
                        AccountMessageKinds.PasswordReset,
                        user!.Email!,
                        user.DisplayName,
                        user.Language,
                        $"/reset-password?user={user.Id}&token={Uri.EscapeDataString(token)}",
                        new Dictionary<string, string?>
                        {
                            ["name"] = user.FirstName,
                            ["minutes"] = ((int)PasswordResetTokenProviderOptions.Lifetime.TotalMinutes).ToString(CultureInfo.InvariantCulture),
                        }),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            reason ??= "email_unavailable";
        }

        // Recorded either way, so repeated requests for one address (or for many) show up in the log.
        await _audit.RecordAsync(
                new AuditEvent
                {
                    Action = IdentityAuditActions.PasswordResetRequested,
                    Outcome = reason is null ? AuditOutcome.Success : AuditOutcome.Failure,
                    Actor = user?.ToAuditSubject() ?? new AuditSubject(AuditSubjectTypes.User, string.Empty, email),
                    Details = reason is null ? null : new Dictionary<string, string?> { ["reason"] = reason },
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Result> CompleteAsync(CompletePasswordResetRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await _userManager.FindByIdAsync(request.UserId.ToString()).ConfigureAwait(false);
        if (user is null || RefusalReason(user) is not null)
        {
            return Result.Failure(UserErrors.PasswordResetInvalid);
        }

        // Checks the token first, so a wrong link is reported as such rather than as a weak password.
        var tokenValid = await _userManager
            .VerifyUserTokenAsync(user, _userManager.Options.Tokens.PasswordResetTokenProvider, UserManager<ApplicationUser>.ResetPasswordTokenPurpose, request.Token)
            .ConfigureAwait(false);

        if (!tokenValid)
        {
            await _audit.RecordAsync(
                    new AuditEvent
                    {
                        Action = IdentityAuditActions.PasswordRecovered,
                        Outcome = AuditOutcome.Failure,
                        Actor = user.ToAuditSubject(),
                        Details = new Dictionary<string, string?> { ["reason"] = "invalid_token" },
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            return Result.Failure(UserErrors.PasswordResetInvalid);
        }

        // Rotates the security stamp: every session of the account ends, and the link cannot be used again.
        var reset = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword).ConfigureAwait(false);
        if (!reset.Succeeded)
        {
            return Result.Failure(reset.ToError(UserErrors.PasswordResetInvalid));
        }

        // The link reached the address, which proves it as well as a confirmation link would.
        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            await _userManager.UpdateAsync(user).ConfigureAwait(false);
        }

        await _userManager.ResetAccessFailedCountAsync(user).ConfigureAwait(false);

        // Whoever knew the old password may have made tokens with it: they go too.
        await _tokens.RevokeAllAsync(user.Id, cancellationToken).ConfigureAwait(false);

        await _audit.RecordAsync(
                new AuditEvent { Action = IdentityAuditActions.PasswordRecovered, Actor = user.ToAuditSubject() },
                cancellationToken)
            .ConfigureAwait(false);

        await PasswordChangeNotice.SendAsync(_messaging, user, cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    /// <summary>Why an account gets no reset link, or null when it may have one.</summary>
    private string? RefusalReason(ApplicationUser user)
    {
        if (user.PendingApproval)
        {
            return "pending_approval";
        }

        if (user.LockoutEnabled && user.LockoutEnd is { } end && end > _clock.UtcNow + AdministrativeLockout)
        {
            return "deactivated";
        }

        return null;
    }
}

/// <summary>Tells an account's owner by email that its password changed, so a change they did not make stands out.</summary>
internal static class PasswordChangeNotice
{
    public static Task<bool> SendAsync(IAccountMessaging messaging, ApplicationUser user, CancellationToken cancellationToken) =>
        messaging.SendAsync(
            new AccountMessage(
                AccountMessageKinds.PasswordChanged,
                user.Email!,
                user.DisplayName,
                user.Language,
                "/account?tab=security",
                new Dictionary<string, string?> { ["name"] = user.FirstName }),
            cancellationToken);
}
