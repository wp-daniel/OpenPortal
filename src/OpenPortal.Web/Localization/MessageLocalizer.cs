using Microsoft.Extensions.Localization;
using OpenPortal.Web.Resources;

namespace OpenPortal.Web.Localization;

/// <summary>
/// Looks a message up for the current request culture and falls back to a supplied English text, so an error
/// whose key has no translation still says something.
/// </summary>
public static class MessageLocalizer
{
    public static string Localize(this HttpContext context, string key, string fallback)
    {
        ArgumentNullException.ThrowIfNull(context);

        var localizer = context.RequestServices.GetService<IStringLocalizer<Messages>>();
        var entry = localizer?[key];

        return entry is { ResourceNotFound: false } ? entry.Value : fallback;
    }
}
