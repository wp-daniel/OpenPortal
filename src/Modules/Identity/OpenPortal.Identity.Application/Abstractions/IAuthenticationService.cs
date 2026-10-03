using OpenPortal.Identity.Application.Contracts;
using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Identity.Application.Abstractions;

/// <summary>
/// Sign-in, sign-out and session inspection.
/// <para>
/// This interface is the entry point of the reusable authentication capability. A module that consumes it
/// never learns which password-hashing library, cookie scheme or user store is in use.
/// </para>
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// Establishes an authenticated session for the supplied credentials.
    /// </summary>
    /// <returns>
    /// <see cref="UserErrors.InvalidCredentials"/> for any failure that must not reveal whether the
    /// account exists. Lockout and "not allowed" states are reported separately because suppressing them
    /// produces a worse experience for legitimate users.
    /// </returns>
    Task<Result> SignInAsync(LoginRequest request, CancellationToken cancellationToken);

    /// <summary>Terminates the current session and clears its authentication cookie.</summary>
    Task SignOutAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The password policy this deployment enforces, read from the configured options rather than restated.
    /// <para>
    /// Exposed separately so that a caller which has to synthesise a session outside the happy path cannot
    /// fabricate one with an invented policy: a client validating against a made-up policy would disagree
    /// with the user store, and the disagreement would surface as an unexplained validation failure.
    /// </para>
    /// </summary>
    PasswordPolicyDto PasswordPolicy { get; }

    /// <summary>
    /// Returns the caller's session state. Always succeeds; an anonymous caller receives
    /// <c>IsAuthenticated = false</c> with a <see langword="null"/> user.
    /// </summary>
    Task<Result<SessionDto>> GetSessionAsync(CancellationToken cancellationToken);
}