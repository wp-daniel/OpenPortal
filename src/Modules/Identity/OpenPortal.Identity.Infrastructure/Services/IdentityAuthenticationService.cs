using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Application.Auditing;
using OpenPortal.Identity.Application.Contracts;
using OpenPortal.Identity.Domain.Users;
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
        IClock clock)
    {
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

        if (!signIn.Succeeded)
        {
            await RecordSignInAsync(user.ToAuditSubject(), FailureReason(signIn), cancellationToken).ConfigureAwait(false);

            return Result.Failure(DescribeFailure(signIn));
        }

        await CompleteSignInAsync(user, method: null, cancellationToken).ConfigureAwait(false);

        return Result.Success();
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
        if (!_currentUser.IsAuthenticated)
        {
            return Result<SessionDto>.Success(SessionDto.AnonymousFor(PasswordPolicy));
        }

        var lookup = await _userLookup.FindCurrentAsync(cancellationToken).ConfigureAwait(false);

        if (lookup.IsFailure)
        {
            // The cookie references an account that no longer exists. Report an anonymous session rather
            // than an error, and let the cookie expire naturally.
            return Result<SessionDto>.Success(SessionDto.AnonymousFor(PasswordPolicy));
        }

        var user = lookup.Value;
        var roles = await _userManager.GetRolesAsync(user).ConfigureAwait(false);
        var pages = await _pages.GetPagesAsync(user.Id, roles.ToArray(), cancellationToken).ConfigureAwait(false);

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
                Pages: pages),
            PasswordPolicy: PasswordPolicy));
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