using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OpenPortal.Web.Infrastructure;
using OpenPortal.Web.Localization;

namespace OpenPortal.Web.Security;

/// <summary>Settings for request rate limits, bound from the <c>RateLimiting</c> configuration section.</summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Off only for a load test or behind a gateway that already limits.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Every request to the API and the OpenID Connect endpoints, per signed-in user or, for anonymous callers,
    /// per address. Generous: it exists to stop a runaway script, not a busy person.
    /// </summary>
    public WindowLimit General { get; set; } = new() { PermitLimit = 600, WindowSeconds = 60 };

    /// <summary>
    /// Password checks (sign-in, change of password), per address. Complements the per-account lockout: lockout
    /// stops guessing one account, this stops trying one password against many.
    /// </summary>
    public WindowLimit SignIn { get; set; } = new() { PermitLimit = 10, WindowSeconds = 60 };

    /// <summary>The token endpoint and application announcements, per address (servers, not browsers).</summary>
    public WindowLimit Machine { get; set; } = new() { PermitLimit = 120, WindowSeconds = 60 };

    public sealed class WindowLimit
    {
        public int PermitLimit { get; set; }

        public int WindowSeconds { get; set; }
    }
}

/// <summary>The named policies endpoints opt into with <c>[EnableRateLimiting]</c>.</summary>
public static class RateLimitPolicies
{
    public const string SignIn = "sign-in";
    public const string Machine = "machine";
}

/// <summary>
/// Request rate limits. Over the limit, the caller gets 429 as <c>application/problem+json</c> with a
/// <c>Retry-After</c> header, on every path, the OpenID Connect ones included.
/// <para>
/// Limits are kept per address, so behind a reverse proxy the forwarded headers must be applied first
/// (<c>ReverseProxy</c> settings); otherwise every caller shares the proxy's address and one limit. The counters
/// live in memory: with several instances each enforces its own, which still bounds what one instance serves.
/// </para>
/// </summary>
public static class RateLimitingSetup
{
    public static IServiceCollection AddOpenPortalRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var options = configuration.GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>() ?? new RateLimitingOptions();
        services.AddSingleton(options);

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.OnRejected = WriteRejectionAsync;

            if (!options.Enabled)
            {
                limiter.AddPolicy(RateLimitPolicies.SignIn, _ => RateLimitPartition.GetNoLimiter(string.Empty));
                limiter.AddPolicy(RateLimitPolicies.Machine, _ => RateLimitPartition.GetNoLimiter(string.Empty));
                return;
            }

            // Static files are served before routing and never reach the limiter; health probes opt out.
            limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                IsLimited(context)
                    ? Window($"general:{CallerKey(context)}", options.General)
                    : RateLimitPartition.GetNoLimiter(string.Empty));

            limiter.AddPolicy(RateLimitPolicies.SignIn, context => Window($"sign-in:{AddressOf(context)}", options.SignIn));
            limiter.AddPolicy(RateLimitPolicies.Machine, context => Window($"machine:{AddressOf(context)}", options.Machine));
        });

        return services;
    }

    private static bool IsLimited(HttpContext context) =>
        context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
        || context.Request.Path.StartsWithSegments("/connect", StringComparison.OrdinalIgnoreCase);

    private static RateLimitPartition<string> Window(string key, RateLimitingOptions.WindowLimit limit) =>
        RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = Math.Max(1, limit.PermitLimit),
            Window = TimeSpan.FromSeconds(Math.Max(1, limit.WindowSeconds)),
            QueueLimit = 0,
            AutoReplenishment = true,
        });

    /// <summary>A signed-in caller is counted as themselves wherever they connect from; anyone else by address.</summary>
    private static string CallerKey(HttpContext context) =>
        context.User.FindFirstValue(ClaimTypes.NameIdentifier) is { } userId ? $"user:{userId}" : $"ip:{AddressOf(context)}";

    private static string AddressOf(HttpContext context) => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static async ValueTask WriteRejectionAsync(OnRejectedContext rejected, CancellationToken cancellationToken)
    {
        var context = rejected.HttpContext;

        if (rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        context.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(RateLimitingSetup))
            .LogWarning("Rate limit reached for {Method} {Path}.", context.Request.Method, context.Request.Path);

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = context.Localize("error.http.too_many_requests", "Too many requests."),
            Instance = context.Request.Path,
            Type = "https://tools.ietf.org/html/rfc6585#section-4",
        };

        problem.Extensions["errorCode"] = "http.too_many_requests";
        problem.Extensions["traceId"] = context.TraceIdentifier;

        await context.Response
            .WriteAsJsonAsync(problem, options: null, contentType: ProblemResults.ProblemJson, cancellationToken)
            .ConfigureAwait(false);
    }
}
