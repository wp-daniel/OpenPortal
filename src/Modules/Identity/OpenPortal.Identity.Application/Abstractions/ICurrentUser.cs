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
}
