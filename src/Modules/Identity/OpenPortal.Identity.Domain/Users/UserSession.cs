namespace OpenPortal.Identity.Domain.Users;

/// <summary>Why a session ended.</summary>
public enum SessionEndReason
{
    /// <summary>Still open.</summary>
    None = 0,

    /// <summary>Its owner signed out.</summary>
    SignedOut = 1,

    /// <summary>Its owner (from another session) or an administrator ended it.</summary>
    Revoked = 2,

    /// <summary>Nothing happened in it for longer than the portal's idle timeout.</summary>
    Idle = 3,

    /// <summary>
    /// The same sign-in was re-issued (a new password, a new second factor, the end of a support session's
    /// re-validation): the session goes on under a new id, so this row is not shown as a separate sign-in.
    /// </summary>
    Replaced = 4,
}

/// <summary>
/// One sign-in to the portal on one browser: the session cookie holds only this row's id, and the authentication
/// ticket lives here, so a session can be listed, and ended at once by its owner or an administrator. Ended rows are
/// kept for a while as the account's sign-in history (and to recognise devices it used before).
/// </summary>
public sealed class UserSession
{
    public const int IdLength = 32;
    public const int IpAddressMaxLength = 64;
    public const int UserAgentMaxLength = 512;
    public const int DeviceMaxLength = 100;
    public const int MethodsMaxLength = 100;

    /// <summary>Required by Entity Framework Core materialisation.</summary>
    private UserSession()
    {
    }

    private UserSession(string id, Guid userId, DateTimeOffset now)
    {
        Id = id;
        UserId = userId;
        CreatedAtUtc = now;
        LastSeenAtUtc = now;
    }

    /// <summary>The random key in the cookie (32 hex characters).</summary>
    public string Id { get; private set; } = string.Empty;

    public Guid UserId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>The last request made with the session (updated at most once a minute).</summary>
    public DateTimeOffset LastSeenAtUtc { get; private set; }

    /// <summary>When the cookie stops being accepted unless it is used (and so renewed) before.</summary>
    public DateTimeOffset? ExpiresAtUtc { get; private set; }

    public DateTimeOffset? EndedAtUtc { get; private set; }

    public SessionEndReason EndReason { get; private set; }

    /// <summary>The address the session was last used from.</summary>
    public string? IpAddress { get; private set; }

    public string? UserAgent { get; private set; }

    /// <summary>A readable summary of <see cref="UserAgent"/> (<c>Firefox on Windows</c>).</summary>
    public string? Device { get; private set; }

    /// <summary>How the session was established (<c>amr</c> values, space separated).</summary>
    public string? Methods { get; private set; }

    /// <summary>The administrator behind a support session, if it is one.</summary>
    public Guid? ImpersonatorId { get; private set; }

    /// <summary>Whether the cookie outlives the browser ("keep me signed in").</summary>
    public bool Persistent { get; private set; }

    /// <summary>The serialised authentication ticket, protected with the portal's Data Protection keys.</summary>
    public byte[] Ticket { get; private set; } = [];

    public bool IsActiveAt(DateTimeOffset now) => EndedAtUtc is null && (ExpiresAtUtc is null || ExpiresAtUtc > now);

    public static UserSession Open(
        string id,
        Guid userId,
        DateTimeOffset now,
        byte[] ticket,
        DateTimeOffset? expiresAtUtc,
        bool persistent,
        string? ipAddress,
        string? userAgent,
        string? methods,
        Guid? impersonatorId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(ticket);

        var session = new UserSession(id, userId, now)
        {
            Ticket = ticket,
            ExpiresAtUtc = expiresAtUtc,
            Persistent = persistent,
            Methods = Clip(methods, MethodsMaxLength),
            ImpersonatorId = impersonatorId,
        };

        session.SeenFrom(ipAddress, userAgent);

        return session;
    }

    /// <summary>Carries the start of the sign-in this session continues (see <see cref="SessionEndReason.Replaced"/>).</summary>
    public void ContinueFrom(UserSession previous)
    {
        ArgumentNullException.ThrowIfNull(previous);

        CreatedAtUtc = previous.CreatedAtUtc;
    }

    /// <summary>A newer ticket for the same session (the cookie's sliding expiry moved on).</summary>
    public void Renew(byte[] ticket, DateTimeOffset? expiresAtUtc)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        Ticket = ticket;
        ExpiresAtUtc = expiresAtUtc;
    }

    /// <summary>Records a request made with the session. Returns whether anything changed.</summary>
    public bool Touch(DateTimeOffset now, string? ipAddress, string? userAgent)
    {
        if (now - LastSeenAtUtc < TimeSpan.FromMinutes(1) && string.Equals(IpAddress, Clip(ipAddress, IpAddressMaxLength), StringComparison.Ordinal))
        {
            return false;
        }

        LastSeenAtUtc = now;
        SeenFrom(ipAddress, userAgent ?? UserAgent);

        return true;
    }

    /// <summary>Ends the session; an ended session stays ended (the first reason wins).</summary>
    public bool End(DateTimeOffset now, SessionEndReason reason)
    {
        if (EndedAtUtc is not null || reason == SessionEndReason.None)
        {
            return false;
        }

        EndedAtUtc = now;
        EndReason = reason;
        Ticket = [];

        return true;
    }

    /// <summary>
    /// The sign-out that ended this row was the re-issue of the same sign-in (<see cref="SessionEndReason.Replaced"/>), not
    /// the owner leaving.
    /// </summary>
    public void MarkReplaced()
    {
        if (EndReason == SessionEndReason.SignedOut)
        {
            EndReason = SessionEndReason.Replaced;
        }
    }

    private void SeenFrom(string? ipAddress, string? userAgent)
    {
        IpAddress = Clip(ipAddress, IpAddressMaxLength);
        UserAgent = Clip(userAgent, UserAgentMaxLength);
        Device = Clip(DeviceNames.Describe(UserAgent), DeviceMaxLength);
    }

    private static string? Clip(string? value, int maxLength)
    {
        var trimmed = value?.Trim();

        return string.IsNullOrEmpty(trimmed) ? null : trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }
}

/// <summary>Turns a user agent into "Browser on System", enough to recognise one's own devices in a list.</summary>
public static class DeviceNames
{
    public static string? Describe(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return null;
        }

        var browser = Browser(userAgent);
        var system = System(userAgent);

        return (browser, system) switch
        {
            (null, null) => null,
            (not null, not null) => $"{browser} on {system}",
            _ => browser ?? system,
        };
    }

    private static string? Browser(string ua) =>
        Has(ua, "Edg/") || Has(ua, "EdgA/") || Has(ua, "EdgiOS/") ? "Edge"
        : Has(ua, "OPR/") || Has(ua, "Opera") ? "Opera"
        : Has(ua, "SamsungBrowser/") ? "Samsung Internet"
        : Has(ua, "Firefox/") || Has(ua, "FxiOS/") ? "Firefox"
        : Has(ua, "Chrome/") || Has(ua, "CriOS/") ? "Chrome"
        : Has(ua, "Safari/") && Has(ua, "Version/") ? "Safari"
        : null;

    private static string? System(string ua) =>
        Has(ua, "iPhone") || Has(ua, "iPad") || Has(ua, "iOS") ? "iOS"
        : Has(ua, "Android") ? "Android"
        : Has(ua, "Windows") ? "Windows"
        : Has(ua, "Mac OS X") || Has(ua, "Macintosh") ? "macOS"
        : Has(ua, "CrOS") ? "ChromeOS"
        : Has(ua, "Linux") ? "Linux"
        : null;

    private static bool Has(string value, string part) => value.Contains(part, StringComparison.OrdinalIgnoreCase);
}
