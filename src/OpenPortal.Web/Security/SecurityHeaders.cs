namespace OpenPortal.Web.Security;

/// <summary>Settings for the security headers, bound from the <c>SecurityHeaders</c> configuration section.</summary>
public sealed class SecurityHeadersOptions
{
    public const string SectionName = "SecurityHeaders";

    /// <summary>
    /// The Content-Security-Policy for the portal's own pages and API. The default allows only this origin;
    /// override it when a deployment adds, for example, an analytics script.
    /// </summary>
    public string ContentSecurityPolicy { get; set; } = DefaultContentSecurityPolicy;

    /// <summary>
    /// Scripts, connections, fonts and frames from this origin only, and the page cannot be framed or post forms
    /// elsewhere.
    /// <para>
    /// Two allowances, both deliberate: <c>style-src 'unsafe-inline'</c>, because the toast library injects its
    /// stylesheet at run time and the error page carries an inline one (style injection cannot run script);
    /// and <c>data:</c>/<c>blob:</c> images, for bundled icons and the avatar crop preview. Address lookups go
    /// through this server, so no third-party origin is needed.
    /// </para>
    /// </summary>
    public const string DefaultContentSecurityPolicy =
        "default-src 'self'; "
        + "script-src 'self'; "
        + "style-src 'self' 'unsafe-inline'; "
        + "img-src 'self' data: blob:; "
        + "font-src 'self' data:; "
        + "connect-src 'self'; "
        + "manifest-src 'self'; "
        + "object-src 'none'; "
        + "base-uri 'self'; "
        + "form-action 'self'; "
        + "frame-ancestors 'none'";
}

/// <summary>
/// Adds the browser security headers to every response: what the page may load (CSP), that it may not be framed
/// (clickjacking on the sign-in page would hand out every application's password), no MIME sniffing, no
/// referrer leaving the portal, and no powerful browser features.
/// <para>
/// The OpenID Connect endpoints get a reduced set: they answer other applications (redirects, JSON, and a
/// self-submitting form when an application asks for <c>form_post</c>), so a policy written for the SPA would
/// break them, but they still refuse to be framed. HSTS is added by <c>UseHsts</c> outside Development.
/// </para>
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private const string PermissionsPolicy =
        "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=(), browsing-topics=()";

    private readonly RequestDelegate _next;
    private readonly string _contentSecurityPolicy;

    public SecurityHeadersMiddleware(RequestDelegate next, Microsoft.Extensions.Options.IOptions<SecurityHeadersOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _next = next;
        _contentSecurityPolicy = string.IsNullOrWhiteSpace(options.Value.ContentSecurityPolicy)
            ? SecurityHeadersOptions.DefaultContentSecurityPolicy
            : options.Value.ContentSecurityPolicy;
    }

    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Set as the response starts, so the headers reach every response, error pages and static files included,
        // and an endpoint that chose its own value (the avatar's cache policy) keeps it.
        context.Response.OnStarting(() =>
        {
            Apply(context);
            return Task.CompletedTask;
        });

        return _next(context);
    }

    private void Apply(HttpContext context)
    {
        var headers = context.Response.Headers;
        var path = context.Request.Path;
        var isProtocolEndpoint = path.StartsWithSegments("/connect", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/.well-known", StringComparison.OrdinalIgnoreCase);

        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Permissions-Policy"] = PermissionsPolicy;
        headers["Cross-Origin-Opener-Policy"] = "same-origin";

        if (isProtocolEndpoint)
        {
            headers.ContentSecurityPolicy = "frame-ancestors 'none'";
            return;
        }

        headers.ContentSecurityPolicy = _contentSecurityPolicy;
        headers["Cross-Origin-Resource-Policy"] = "same-origin";

        // API answers carry account data; no shared or browser cache should keep them unless the endpoint says
        // otherwise (the avatar does).
        if (path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase) && !headers.ContainsKey("Cache-Control"))
        {
            headers.CacheControl = "no-store";
        }
    }
}
