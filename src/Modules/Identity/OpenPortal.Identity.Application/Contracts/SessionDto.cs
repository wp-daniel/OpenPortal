namespace OpenPortal.Identity.Application.Contracts;

/// <summary>
/// The public projection of a session, safe to hand to a browser.
/// <para>
/// Only the fields the UI renders are present. Password hashes, security stamps, concurrency stamps,
/// lockout counters and confirmation tokens are deliberately absent and must never be added: a client has
/// no use for them and they are an unnecessary disclosure risk.
/// </para>
/// </summary>
public sealed record SessionUserDto(
    Guid Id,
    string Email,
    string DisplayName,
    bool EmailConfirmed,
    IReadOnlyList<string> Roles);

/// <summary>Current session state as observed by the client.</summary>
/// <param name="IsAuthenticated">Whether a valid session cookie was presented.</param>
/// <param name="User">The caller, or <see langword="null"/> when anonymous.</param>
/// <param name="PasswordPolicy">
/// The policy this deployment enforces. Sent to anonymous callers too, because the sign-in and
/// change-password forms need it before anyone is signed in. It discloses configuration, not data.
/// </param>
public sealed record SessionDto(bool IsAuthenticated, SessionUserDto? User, PasswordPolicyDto PasswordPolicy)
{
    /// <summary>
    /// The anonymous session. Takes the policy as an argument because there is no default that is honest:
    /// the policy is deployment configuration, so a fabricated one could only ever disagree with the server.
    /// </summary>
    public static SessionDto AnonymousFor(PasswordPolicyDto passwordPolicy) => new(false, null, passwordPolicy);
}

/// <summary>
/// The password policy the server actually enforces. Published so that client-side forms can validate
/// against the real rules instead of a hardcoded copy that drifts.
/// </summary>
public sealed record PasswordPolicyDto(
    int RequiredLength,
    int RequiredUniqueChars,
    bool RequireLowercase,
    bool RequireUppercase,
    bool RequireDigit,
    bool RequireNonAlphanumeric);

/// <summary>
/// The antiforgery token for the current browser session.
/// <para>
/// The token is a per-session nonce, not a credential: it proves only that the request came from a page this
/// origin served, so it is safe to hand to script. The session cookie that gives it meaning stays HttpOnly.
/// </para>
/// </summary>
/// <param name="RequestToken">Value to echo in the <c>X-XSRF-TOKEN</c> request header.</param>
/// <param name="HeaderName">Name of the header the token must be sent in.</param>
public sealed record AntiforgeryTokenDto(string RequestToken, string HeaderName);