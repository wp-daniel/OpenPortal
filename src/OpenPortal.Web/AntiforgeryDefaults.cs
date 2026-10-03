namespace OpenPortal.Web;

/// <summary>
/// The antiforgery cookie and header names, defined once because three places must agree on them: the
/// antiforgery configuration, the token endpoint that tells the client what to send, and the client itself.
/// </summary>
public static class AntiforgeryDefaults
{
    /// <summary>
    /// Readable by script on purpose. The value is a per-session nonce, not a credential: it proves only
    /// that a request originated from a page this origin served. The session cookie that gives it meaning
    /// stays HttpOnly.
    /// </summary>
    public const string CookieName = "XSRF-TOKEN";

    public const string HeaderName = "X-XSRF-TOKEN";
}