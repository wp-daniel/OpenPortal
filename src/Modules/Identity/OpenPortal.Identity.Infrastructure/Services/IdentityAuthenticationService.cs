using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Application.Auditing;
using OpenPortal.Identity.Application.Contracts;
using OpenPortal.Identity.Domain.Users;
using OpenPortal.Identity.Infrastructure.Configuration;
using OpenPortal.SharedKernel.Auditing;
using OpenPortal.SharedKernel.Results;
using OpenPortal.SharedKernel.Time;

namespace OpenPortal.Identity.Infrastructure.Services;

/// <inheritdoc />
internal sealed class IdentityAuthenticationService : IAuthenticationService
{
    /// <summary>
    /// Compared against when the email is unknown, so that a missing account costs the same amount of
    /// work as a wrong password. Without this, response latency reveals which addresses are registered.
    /// </summary>
    private const string TimingEqualisationPassword = "OpenPortal::timing-equalisation";

    private static readonly ApplicationUser TimingEqualisationUser = ApplicationUser.Create(
        Guid.Empty,
        "timing-equalisation@invalid.local",
        new UserDetails("Timing", "Equalisation", null, null, null, null, null, null, null, null),
        DateTimeOffset.UnixEpoch).Value;

    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPasswordHasher<ApplicationUser> _passwordHasher;
    private readonly UserLookup _userLookup;
    private readonly ICurrentUser _currentUser;
    private readonly IUserPageSource _pages;
    private readonly IOptions<IdentityOptions> _identityOptions;
    private readonly IAuditTrail _audit;
    private readonly IClock _clock;
    private readonly RegistrationSettingsStore _registration;
    private readonly IdentityLegalDocumentService _legal;
    private readonly PrivacySettingsStore _privacy;
    private readonly IOptions<IdentityModuleOptions> _moduleOptions;
    private readonly SecuritySettingsStore _security;
    private readonly IAccountMessaging _messaging;
    private readonly IdentityTwoFactorPolicy _policy;
    private readonly Lazy<string> _timingEqualisationHash;

    public IdentityAuthenticationService(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IPasswordHasher<ApplicationUser> passwordHasher,
        UserLookup userLookup,
        ICurrentUser currentUser,
        IUserPageSource pages,
        IOptions<IdentityOptions> identityOptions,
        IAuditTrail audit,
        IClock clock,
        RegistrationSettingsStore registration,
        IdentityLegalDocumentService legal,
        PrivacySettingsStore privacy,
        IOptions<IdentityModuleOptions> moduleOptions,
        SecuritySettingsStore security,
        IAccountMessaging messaging,
        IdentityTwoFactorPolicy policy)
    {
        _policy = policy;
        _messaging = messaging;
        _security = security;
        _legal = legal;
        _privacy = privacy;
        _moduleOptions = moduleOptions;
        _registration = registration;
        _signInManager = signInManager;
        _userManager = userManager;
        _passwordHasher = passwordHasher;
        _userLookup = userLookup;
        _currentUser = currentUser;
        _pages = pages;
        _identityOptions = identityOptions;
        _audit = audit;
        _clock = clock;
        _timingEqualisationHash = new Lazy<string>(
            () => _passwordHasher.HashPassword(TimingEqualisationUser, TimingEqualisationPassword),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>
    /// Reads the policy from the configured options rather than from a constant, so a client validating
    /// against this can never disagree with what the user store enforces.
    /// </summary>
    public PasswordPolicyDto PasswordPolicy
    {
        get
        {
            var password = _identityOptions.Value.Password;

            return new PasswordPolicyDto(
                RequiredLength: password.RequiredLength,
                RequiredUniqueChars: password.RequiredUniqueChars,
                RequireLowercase: password.RequireLowercase,
                RequireUppercase: password.RequireUppercase,
                RequireDigit: password.RequireDigit,
                RequireNonAlphanumeric: password.RequireNonAlphanumeric);
        }
    }

    public async Task<Result> SignInAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var user = await _userManager
            .FindByEmailAsync(request.Email.Trim())
            .ConfigureAwait(false);

        if (user is null)
        {
            // The verification result is deliberately discarded: this call exists only to burn the same
            // CPU time a real password check would, hiding whether the address is registered.
            _ = _passwordHasher.VerifyHashedPassword(
                TimingEqualisationUser,
                _timingEqualisationHash.Value,
                request.Password);

            // The address is recorded as typed (there is no account to name), so repeated guesses against
            // unknown addresses are visible in the log too.
            await RecordSignInAsync(
                    new AuditSubject(AuditSubjectTypes.User, string.Empty, request.Email.Trim()),
                    SignInFailureReasons.UnknownAccount,
                    cancellationToken)
                .ConfigureAwait(false);

            return Result.Failure(UserErrors.InvalidCredentials);
        }

        // An account whose only second factor is a passkey, and that must have one, cannot sign in with its
        // password alone. The password is still checked (and counts toward the lockout), so the answer reveals
        // the rule only to whoever knows it.
        if (await _policy.RequiresPasskeySignInAsync(user, cancellationToken).ConfigureAwait(false))
        {
            var check = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true).ConfigureAwait(false);
            if (check.Succeeded)
            {
                await RecordSignInAsync(user.ToAuditSubject(), SignInFailureReasons.PasskeyRequired, cancellationToken).ConfigureAwait(false);

                return Result.Failure(UserErrors.PasskeyRequired);
            }

            if (check.IsNotAllowed)
            {
                return await RefuseNotAllowedAsync(user, request.Password, cancellationToken).ConfigureAwait(false);
            }

            await RecordSignInAsync(user.ToAuditSubject(), FailureReason(check), cancellationToken).ConfigureAwait(false);

            return Result.Failure(DescribeFailure(check));
        }

        // SignInManager (rather than a manual hash comparison) is what provides account lockout and
        // security-stamp validation. Neither overload accepts a CancellationToken.
        var signIn = await _signInManager
            .PasswordSignInAsync(user, request.Password, isPersistent: request.RememberMe, lockoutOnFailure: true)
            .ConfigureAwait(false);

        // The password was right but the second step is still to come: that step records the outcome, so a
        // correct password is not logged as a failed sign-in.
        if (signIn.RequiresTwoFactor)
        {
            return Result.Failure(UserErrors.TwoFactorRequired);
        }

        if (signIn.IsNotAllowed)
        {
            return await RefuseNotAllowedAsync(user, request.Password, cancellationToken).ConfigureAwait(false);
        }

        if (!signIn.Succeeded)
        {
            await RecordSignInAsync(user.ToAuditSubject(), FailureReason(signIn), cancellationToken).ConfigureAwait(false);

            return Result.Failure(DescribeFailure(signIn));
        }

        await CompleteSignInAsync(user, method: null, cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    /// <summary>
    /// The framework refuses an account that may not sign in before it checks the password. Saying why (waiting
    /// for approval, address unconfirmed) is only for whoever knows the password; anybody else gets the answer a
    /// wrong password gets, and the attempt counts toward the lockout like one.
    /// </summary>
    private async Task<Result> RefuseNotAllowedAsync(ApplicationUser user, string password, CancellationToken cancellationToken)
    {
        if (!await _userManager.CheckPasswordAsync(user, password).ConfigureAwait(false))
        {
            await _userManager.AccessFailedAsync(user).ConfigureAwait(false);
            await RecordSignInAsync(user.ToAuditSubject(), SignInFailureReasons.InvalidPassword, cancellationToken).ConfigureAwait(false);

            return Result.Failure(UserErrors.InvalidCredentials);
        }

        var (reason, error) = user.PendingApproval
            ? (SignInFailureReasons.PendingApproval, UserErrors.AccountPendingApproval)
            : await _registration.AwaitsEmailConfirmationAsync(user, cancellationToken).ConfigureAwait(false)
                ? (SignInFailureReasons.EmailNotConfirmed, UserErrors.EmailNotConfirmed)
                : (SignInFailureReasons.NotAllowed, UserErrors.AccountNotAllowed);

        await RecordSignInAsync(user.ToAuditSubject(), reason, cancellationToken).ConfigureAwait(false);

        return Result.Failure(error);
    }

    public async Task<Result> SignInWithTwoFactorAsync(TwoFactorLoginRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        // Read from the short-lived cookie the password step wrote; gone once it expires.
        var user = await _signInManager.GetTwoFactorAuthenticationUserAsync().ConfigureAwait(false);
        if (user is null)
        {
            return Result.Failure(UserErrors.TwoFactorSessionExpired);
        }

        // Recovery codes keep their dash ("XXXXX-XXXXX", upper case); app codes are digits however they were typed.
        var code = request.UseRecoveryCode
            ? request.Code.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant()
            : IdentityTwoFactorService.NormaliseCode(request.Code);

        // Both count a wrong code as a failed attempt, so guessing codes runs into the same lockout as guessing
        // passwords. A recovery code is consumed by a successful use. Neither overload accepts a token.
        var signIn = request.UseRecoveryCode
            ? await _signInManager.TwoFactorRecoveryCodeSignInAsync(code).ConfigureAwait(false)
            : await _signInManager
                .TwoFactorAuthenticatorSignInAsync(code, request.RememberMe, request.RememberBrowser)
                .ConfigureAwait(false);

        var method = request.UseRecoveryCode ? SignInMethods.RecoveryCode : SignInMethods.Authenticator;

        if (!signIn.Succeeded)
        {
            await RecordSignInAsync(
                    user.ToAuditSubject(),
                    signIn.IsLockedOut ? SignInFailureReasons.LockedOut : SignInFailureReasons.InvalidCode,
                    cancellationToken,
                    method)
                .ConfigureAwait(false);

            return Result.Failure(signIn.IsLockedOut ? UserErrors.AccountLockedOut : UserErrors.TwoFactorCodeInvalid);
        }

        await CompleteSignInAsync(user, method, cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    public async Task<Result<string>> PasskeyRequestOptionsAsync(CancellationToken cancellationToken)
    {
        if (!(await _security.ReadAsync(cancellationToken).ConfigureAwait(false)).PasskeysEnabled)
        {
            return Result<string>.Failure(UserErrors.PasskeysDisabled);
        }

        // No user: the browser offers every passkey it holds for this portal (discoverable credentials).
        return Result<string>.Success(await _signInManager.MakePasskeyRequestOptionsAsync(null).ConfigureAwait(false));
    }

    public async Task<Result> SignInWithPasskeyAsync(PasskeySignInRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!(await _security.ReadAsync(cancellationToken).ConfigureAwait(false)).PasskeysEnabled)
        {
            return Result.Failure(UserErrors.PasskeysDisabled);
        }

        // Verifies the signature against the challenge issued last (kept in a short-lived cookie) and the stored key.
        var assertion = await _signInManager.PerformPasskeyAssertionAsync(request.Credential).ConfigureAwait(false);
        if (!assertion.Succeeded)
        {
            await RecordSignInAsync(
                    new AuditSubject(AuditSubjectTypes.User, string.Empty, null),
                    SignInFailureReasons.InvalidPasskey,
                    cancellationToken,
                    SignInMethods.Passkey)
                .ConfigureAwait(false);

            return Result.Failure(UserErrors.PasskeySignInFailed);
        }

        var user = assertion.User;

        // The same checks as a password sign-in: approval, confirmed address, lockout.
        if (!await _signInManager.CanSignInAsync(user).ConfigureAwait(false)
            || await _userManager.IsLockedOutAsync(user).ConfigureAwait(false))
        {
            var (reason, error) = user.PendingApproval
                ? (SignInFailureReasons.PendingApproval, UserErrors.AccountPendingApproval)
                : await _registration.AwaitsEmailConfirmationAsync(user, cancellationToken).ConfigureAwait(false)
                    ? (SignInFailureReasons.EmailNotConfirmed, UserErrors.EmailNotConfirmed)
                    : await _userManager.IsLockedOutAsync(user).ConfigureAwait(false)
                        ? (SignInFailureReasons.LockedOut, UserErrors.AccountLockedOut)
                        : (SignInFailureReasons.NotAllowed, UserErrors.AccountNotAllowed);

            await RecordSignInAsync(user.ToAuditSubject(), reason, cancellationToken, SignInMethods.Passkey).ConfigureAwait(false);

            return Result.Failure(error);
        }

        // The signature counter moves on with every use, which is how a cloned authenticator would show.
        await _userManager.AddOrUpdatePasskeyAsync(user, assertion.Passkey).ConfigureAwait(false);

        // A passkey proves possession of the device and, with user verification required, the person: both
        // factors at once (RFC 8176 hwk and user, hence mfa), so no authenticator code is asked for.
        await _signInManager
            .SignInWithClaimsAsync(
                user,
                request.RememberMe,
                [
                    new Claim(AuthenticationMethods.ClaimType, AuthenticationMethods.HardwareKey),
                    new Claim(AuthenticationMethods.ClaimType, AuthenticationMethods.UserPresence),
                    new Claim(AuthenticationMethods.ClaimType, AuthenticationMethods.MultiFactor),
                ])
            .ConfigureAwait(false);

        await _userManager.ResetAccessFailedCountAsync(user).ConfigureAwait(false);
        await CompleteSignInAsync(user, SignInMethods.Passkey, cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    private async Task CompleteSignInAsync(ApplicationUser user, string? method, CancellationToken cancellationToken)
    {
        user.RecordSignIn(_clock.UtcNow);

        // The session is established either way; a missed timestamp only makes the account look idler than
        // it is, which is not worth failing a sign-in over.
        await _userManager.UpdateAsync(user).ConfigureAwait(false);

        await RecordSignInAsync(user.ToAuditSubject(), failureReason: null, cancellationToken, method).ConfigureAwait(false);
    }

    public async Task SignOutAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Issues a sign-out cookie; it does not redirect, so no cancellation token is needed.
        await _signInManager.SignOutAsync().ConfigureAwait(false);

        // The request still carries the caller's identity, so the trail names who signed out.
        if (_currentUser.IsAuthenticated)
        {
            await _audit.RecordAsync(AuditEvent.Succeeded(IdentityAuditActions.SignOut), cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<Result<SessionDto>> GetSessionAsync(CancellationToken cancellationToken)
    {
        var settings = await _registration.ReadAsync(cancellationToken).ConfigureAwait(false);
        var security = await _security.ReadAsync(cancellationToken).ConfigureAwait(false);
        var registration = new RegistrationInfoDto(
            settings.IsOpen,
            settings.Mode == Domain.Settings.RegistrationMode.Approval,
            PasswordReset: await _messaging.CanSendEmailAsync(cancellationToken).ConfigureAwait(false),
            Passkeys: security.PasskeysEnabled);
        var legal = await _legal.GetInfoAsync(cancellationToken).ConfigureAwait(false);

        if (!_currentUser.IsAuthenticated)
        {
            return Result<SessionDto>.Success(SessionDto.AnonymousFor(PasswordPolicy, registration, legal));
        }

        var lookup = await _userLookup.FindCurrentAsync(cancellationToken).ConfigureAwait(false);

        if (lookup.IsFailure)
        {
            // The cookie references an account that no longer exists. Report an anonymous session rather
            // than an error, and let the cookie expire naturally.
            return Result<SessionDto>.Success(SessionDto.AnonymousFor(PasswordPolicy, registration, legal));
        }

        var user = lookup.Value;
        var roles = await _userManager.GetRolesAsync(user).ConfigureAwait(false);
        var pages = await _pages.GetPagesAsync(user.Id, roles.ToArray(), cancellationToken).ConfigureAwait(false);
        var pendingLegal = await _legal.PendingAsync(user.Id, cancellationToken).ConfigureAwait(false);
        var privacy = await _privacy.ReadAsync(cancellationToken).ConfigureAwait(false);
        var impersonator = _currentUser.ImpersonatorId is { } impersonatorId && _currentUser.ImpersonationStartedAtUtc is { } startedAt
            ? new SessionImpersonatorDto(
                impersonatorId,
                _currentUser.ImpersonatorEmail,
                startedAt,
                startedAt.AddMinutes(_moduleOptions.Value.ImpersonationMinutes))
            : null;

        return Result<SessionDto>.Success(new SessionDto(
            IsAuthenticated: true,
            User: new SessionUserDto(
                Id: user.Id,
                Email: user.Email ?? string.Empty,
                DisplayName: user.DisplayName,
                EmailConfirmed: user.EmailConfirmed,
                Roles: roles.Order(StringComparer.Ordinal).ToArray(),
                Language: user.Language,
                AvatarUpdatedAtUtc: user.AvatarUpdatedAtUtc,
                Pages: pages,
                PendingLegalDocuments: pendingLegal,
                DeletionScheduledAtUtc: user.DeletionScheduledAtUtc,
                Impersonator: impersonator,
                TwoFactorSetupRequired: impersonator is null
                    && await _policy.IsSetupRequiredAsync(user.Id, cancellationToken).ConfigureAwait(false),
                AuthenticationMethods: _currentUser.AuthenticationMethods),
            PasswordPolicy: PasswordPolicy,
            Registration: registration,
            Legal: legal,
            Features: new SessionFeaturesDto(
                SelfDeletion: privacy.SelfDeletionEnabled,
                PersonalAccessTokens: security.PersonalAccessTokensEnabled)));
    }

    private Task RecordSignInAsync(
        AuditSubject actor,
        string? failureReason,
        CancellationToken cancellationToken,
        string? method = null)
    {
        var details = new Dictionary<string, string?>();

        if (failureReason is not null)
        {
            details["reason"] = failureReason;
        }

        // Only present when a second factor was asked for, so the log shows which sign-ins used one.
        if (method is not null)
        {
            details["method"] = method;
        }

        IdentityMetrics.SignIns.Add(
            1,
            new KeyValuePair<string, object?>("outcome", failureReason is null ? "success" : "failure"),
            new KeyValuePair<string, object?>("method", method ?? "password"),
            new KeyValuePair<string, object?>("reason", failureReason ?? "none"));

        return _audit.RecordAsync(
            new AuditEvent
            {
                Action = IdentityAuditActions.SignIn,
                Outcome = failureReason is null ? AuditOutcome.Success : AuditOutcome.Failure,

                // The caller is not signed in yet (or not any more), so the account is named explicitly.
                Actor = actor,
                Details = details.Count == 0 ? null : details,
            },
            cancellationToken);
    }

    private static string FailureReason(SignInResult signIn) => signIn switch
    {
        { IsLockedOut: true } => SignInFailureReasons.LockedOut,
        { IsNotAllowed: true } => SignInFailureReasons.NotAllowed,
        _ => SignInFailureReasons.InvalidPassword,
    };

    private static Error DescribeFailure(SignInResult signIn) => signIn switch
    {
        { IsLockedOut: true } => UserErrors.AccountLockedOut,
        { IsNotAllowed: true } => UserErrors.AccountNotAllowed,
        _ => UserErrors.InvalidCredentials,
    };
}

/// <summary>The Identity module's counters for OpenTelemetry (meter <c>OpenPortal.Identity</c>).</summary>
internal static class IdentityMetrics
{
    public const string MeterName = "OpenPortal.Identity";

    private static readonly System.Diagnostics.Metrics.Meter Meter = new(MeterName);

    /// <summary>Portal sign-ins by outcome, method (password, authenticator, recovery_code, passkey) and failure reason.</summary>
    public static readonly System.Diagnostics.Metrics.Counter<long> SignIns =
        Meter.CreateCounter<long>("openportal.sign_ins", description: "Portal sign-in attempts.");
}
