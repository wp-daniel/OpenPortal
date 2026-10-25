using Microsoft.AspNetCore.Http;

namespace OpenPortal.Identity.Infrastructure.Configuration;

/// <summary>What the host tells the session store about its requests.</summary>
public sealed class SessionStoreOptions
{
    /// <summary>
    /// Whether a request is made by the page on its own (polling, a notification stream) rather than by the person:
    /// such requests do not count as activity for the idle timeout.
    /// </summary>
    public Func<HttpContext, bool> IsBackgroundRequest { get; set; } = _ => false;

    /// <summary>How long ended sessions are kept as the account's sign-in history.</summary>
    public int HistoryDays { get; set; } = 30;
}
