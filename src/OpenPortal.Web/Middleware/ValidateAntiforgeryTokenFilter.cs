using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc.Filters;

namespace OpenPortal.Web.Middleware;

/// <summary>
/// Requires a valid antiforgery token on every unsafe request.
/// <para>
/// Applied globally rather than per action. The API authenticates with a cookie, which a browser sends
/// automatically on cross-site requests, so without this check any site could make a signed-in
/// administrator's browser create users or edit content. A per-action attribute would be one forgotten
/// <c>[ValidateAntiForgeryToken]</c> away from that.
/// </para>
/// <para>
/// Safe methods are exempt, as is the antiforgery endpoint itself.
/// </para>
/// </summary>
public sealed class ValidateAntiforgeryTokenFilter : IAsyncAuthorizationFilter, IOrderedFilter
{
    private static readonly HashSet<string> SafeMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Get,
        HttpMethods.Head,
        HttpMethods.Options,
        HttpMethods.Trace,
    };

    private readonly IAntiforgery _antiforgery;

    public ValidateAntiforgeryTokenFilter(IAntiforgery antiforgery)
    {
        _antiforgery = antiforgery;
    }

    public int Order => int.MinValue + 100;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (SafeMethods.Contains(context.HttpContext.Request.Method))
        {
            return;
        }

        // Awaited, not fired and forgotten. ValidateRequestAsync reports a missing or stale token by
        // throwing AntiforgeryValidationException, and dropping the task would both swallow that exception
        // and let the request through unchecked - which would turn this filter into a silent no-op that
        // still looks like a CSRF defence in review.
        await _antiforgery.ValidateRequestAsync(context.HttpContext).ConfigureAwait(false);
    }
}