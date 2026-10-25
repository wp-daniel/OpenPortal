namespace OpenPortal.Identity.Application.Abstractions;

/// <summary>
/// Read-only view of the caller as established by the host's authentication middleware.
/// Implemented in the Web layer by reading <c>HttpContext.User</c>; consumed by application services so
/// that "who is acting" is an explicit dependency rather than ambient static state.
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>The primary key of the caller, or <see langword="null"/> when anonymous.</summary>
    Guid? UserId { get; }

    string? Email { get; }

    /// <summary>Roles carried by the current session's claims.</summary>
    IReadOnlyCollection<string> Roles { get; }

    /// <summary>
    /// The administrator really at the keyboard while they impersonate <see cref="UserId"/> for support; null in
    /// an ordinary session.
    /// </summary>
    Guid? ImpersonatorId { get; }

    string? ImpersonatorEmail { get; }

    /// <summary>When the impersonation started; null in an ordinary session.</summary>
    DateTimeOffset? ImpersonationStartedAtUtc { get; }

    /// <summary>The personal access token the request authenticated with; null for a browser session.</summary>
    Guid? PersonalAccessTokenId { get; }

    /// <summary>The session's <c>amr</c> values (see <see cref="Abstractions.AuthenticationMethods"/>).</summary>
    IReadOnlyList<string> AuthenticationMethods { get; }

    /// <summary>The portal session (cookie) the request came with; null for a token or an anonymous caller.</summary>
    string? SessionId { get; }

    /// <summary>
    /// When the person last proved who they are in this session (password, passkey, external provider), unchanged by
    /// refreshed sign-ins; null for a token, an anonymous caller or a session older than this value.
    /// </summary>
    DateTimeOffset? AuthenticatedAtUtc { get; }
}

/// <summary>Where the session store leaves the id of the session a request authenticated with.</summary>
public static class SessionItems
{
    public const string CurrentSessionId = "openportal.session_id";
}

/// <summary>
/// The portal's own claims, beyond Identity's. They are issued only by the portal (the session cookie and the
/// personal access token handler), never taken from a request.
/// </summary>
public static class PortalClaimTypes
{
    public const string ImpersonatorId = "portal_impersonator_id";
    public const string ImpersonatorEmail = "portal_impersonator_email";

    /// <summary>The impersonation's start, as Unix seconds.</summary>
    public const string ImpersonationStarted = "portal_impersonation_started";

    /// <summary>The id of the personal access token a request authenticated with.</summary>
    public const string PersonalAccessTokenId = "portal_pat_id";

    /// <summary>One claim per scope the personal access token holds.</summary>
    public const string PersonalAccessTokenScope = "portal_pat_scope";

    /// <summary>
    /// When the person authenticated, as Unix seconds: written at sign-in and kept when the session is re-issued
    /// (a refreshed sign-in, the security stamp check), so it is what OpenID Connect calls <c>auth_time</c>.
    /// </summary>
    public const string AuthenticationTime = "portal_auth_time";
}

/// <summary>
/// How the session was authenticated: the <c>amr</c> claim (RFC 8176) on the portal cookie, carried into the tokens
/// applications receive. Identity writes <c>pwd</c> for a password and <c>mfa</c> after an authenticator code; the
/// portal adds the passkey values.
/// </summary>
public static class AuthenticationMethods
{
    public const string ClaimType = "amr";

    public const string Password = "pwd";
    public const string OneTimePassword = "otp";
    public const string HardwareKey = "hwk";
    public const string UserPresence = "user";
    public const string MultiFactor = "mfa";

    /// <summary>
    /// Signed in through an external identity provider (Google, Microsoft). Not one of RFC 8176's values, which name
    /// how the person proved who they are; the portal does not see how the provider checked them.
    /// </summary>
    public const string Federated = "fed";
}
