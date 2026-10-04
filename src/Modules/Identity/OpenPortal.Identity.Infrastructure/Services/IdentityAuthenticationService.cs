using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Application.Contracts;
using OpenPortal.Identity.Domain.Users;
using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Identity.Infrastructure.Services;

/// <inheritdoc />
internal sealed class IdentityAuthenticationService : IAuthenticationService
{
    /// <summary>
    /// Compared against when the email is unknown, so that a missing account costs the same amount of
    /// work as a wrong password. Without this, response latency reveals which addresses are registered.
    /// </summary>
    private const string TimingEqualisationPassword = "OpenPortal::timing-equalisation";

    private static readonly ApplicationUser TimingEqualisationUser = new(
        Guid.Empty,
        "timing-equalisation@invalid.local",
        "Timing Equalisation",
        DateTimeOffset.UnixEpoch);

    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPasswordHasher<ApplicationUser> _passwordHasher;
    private readonly UserLookup _userLookup;
    private readonly ICurrentUser _currentUser;
    private readonly IOptions<IdentityOptions> _identityOptions;
    private readonly Lazy<string> _timingEqualisationHash;

    public IdentityAuthenticationService(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IPasswordHasher<ApplicationUser> passwordHasher,
        UserLookup userLookup,
        ICurrentUser currentUser,
        IOptions<IdentityOptions> identityOptions)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _passwordHasher = passwordHasher;
        _userLookup = userLookup;
        _currentUser = currentUser;
        _identityOptions = identityOptions;
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

            return Result.Failure(UserErrors.InvalidCredentials);
        }

        // SignInManager (rather than a manual hash comparison) is what provides account lockout and
        // security-stamp validation. Neither overload accepts a CancellationToken.
        var signIn = await _signInManager
            .PasswordSignInAsync(user, request.Password, isPersistent: request.RememberMe, lockoutOnFailure: true)
            .ConfigureAwait(false);

        return signIn.Succeeded ? Result.Success() : Result.Failure(DescribeFailure(signIn));
    }

    public async Task SignOutAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Issues a sign-out cookie; it does not redirect, so no cancellation token is needed.
        await _signInManager.SignOutAsync().ConfigureAwait(false);
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

        return Result<SessionDto>.Success(new SessionDto(
            IsAuthenticated: true,
            User: new SessionUserDto(
                Id: user.Id,
                Email: user.Email ?? string.Empty,
                DisplayName: user.DisplayName,
                EmailConfirmed: user.EmailConfirmed,
                Roles: roles.Order(StringComparer.Ordinal).ToArray(),
                Language: user.Language,
                AvatarUpdatedAtUtc: user.AvatarUpdatedAtUtc),
            PasswordPolicy: PasswordPolicy));
    }

    private static Error DescribeFailure(SignInResult signIn) => signIn switch
    {
        { IsLockedOut: true } => UserErrors.AccountLockedOut,
        { IsNotAllowed: true } => UserErrors.AccountNotAllowed,
        { RequiresTwoFactor: true } => UserErrors.TwoFactorRequired,
        _ => UserErrors.InvalidCredentials,
    };
}