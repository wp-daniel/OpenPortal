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
}