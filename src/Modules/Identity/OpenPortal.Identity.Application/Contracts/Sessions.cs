namespace OpenPortal.Identity.Application.Contracts;

/// <summary>One sign-in to the portal on one browser.</summary>
/// <param name="Id">The session's id (not the cookie itself, which cannot be rebuilt from it).</param>
/// <param name="Device">A readable summary of the browser and system (<c>Firefox on Windows</c>).</param>
/// <param name="Methods">How it was established (<c>pwd</c>, <c>mfa</c>, <c>hwk</c>, <c>fed</c>…).</param>
/// <param name="EndReason">Why it ended: <c>signed_out</c>, <c>revoked</c>, <c>idle</c>, <c>expired</c>; null while open.</param>
/// <param name="Current">The session this request came with.</param>
/// <param name="SupportSession">An administrator opened it to act as the account.</param>
public sealed record SessionInfoDto(
    string Id,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastSeenAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    DateTimeOffset? EndedAtUtc,
    string? EndReason,
    string? IpAddress,
    string? Device,
    IReadOnlyList<string> Methods,
    bool Current,
    bool SupportSession,
    bool Persistent);

/// <summary>An account's open sessions, and the ones that ended recently (its sign-in history).</summary>
public sealed record AccountSessionsDto(IReadOnlyList<SessionInfoDto> Active, IReadOnlyList<SessionInfoDto> Recent);

/// <summary>The contract names of the reasons a session ended.</summary>
public static class SessionEndReasons
{
    public const string SignedOut = "signed_out";
    public const string Revoked = "revoked";
    public const string Idle = "idle";
    public const string Expired = "expired";

    public static string? ToContract(Domain.Users.UserSession session, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(session);

        return session.EndReason switch
        {
            Domain.Users.SessionEndReason.SignedOut or Domain.Users.SessionEndReason.Replaced => SignedOut,
            Domain.Users.SessionEndReason.Revoked => Revoked,
            Domain.Users.SessionEndReason.Idle => Idle,
            _ => session.IsActiveAt(now) ? null : Expired,
        };
    }
}
