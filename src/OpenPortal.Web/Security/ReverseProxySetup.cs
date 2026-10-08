using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace OpenPortal.Web.Security;

/// <summary>Settings for running behind a reverse proxy, bound from the <c>ReverseProxy</c> configuration section.</summary>
public sealed class ReverseProxyOptions
{
    public const string SectionName = "ReverseProxy";

    /// <summary>
    /// Applies <c>X-Forwarded-For</c> and <c>X-Forwarded-Proto</c>, so the client's address (audit log, rate
    /// limits) and the https scheme (secure cookies, OpenID Connect issuer and redirects) are the real ones.
    /// Enable only when every request arrives through the proxy: anyone who can reach the portal directly could
    /// otherwise claim any address.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Addresses of the proxies to trust (for example <c>10.0.0.5</c>). With neither this nor
    /// <see cref="KnownNetworks"/>, any sender is trusted, which suits a container reachable only through its
    /// ingress.
    /// </summary>
    public IList<string> KnownProxies { get; } = [];

    /// <summary>Networks of the proxies to trust, in CIDR notation (for example <c>10.0.0.0/8</c>).</summary>
    public IList<string> KnownNetworks { get; } = [];

    /// <summary>How many proxies a request passes through; each one appends to the forwarded headers.</summary>
    public int ForwardLimit { get; set; } = 1;
}

/// <summary>Reads the client's address and scheme from a trusted reverse proxy's forwarded headers.</summary>
public static class ReverseProxySetup
{
    public static IServiceCollection AddOpenPortalReverseProxy(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var proxy = configuration.GetSection(ReverseProxyOptions.SectionName).Get<ReverseProxyOptions>() ?? new ReverseProxyOptions();
        services.AddSingleton(proxy);

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = Math.Max(1, proxy.ForwardLimit);

            // The framework trusts only loopback by default. Configured lists replace that; no list at all means
            // "trust the sender", for a deployment where only the proxy can reach the portal.
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();

            foreach (var address in proxy.KnownProxies)
            {
                options.KnownProxies.Add(IPAddress.Parse(address));
            }

            foreach (var network in proxy.KnownNetworks)
            {
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
            }
        });

        return services;
    }

    /// <summary>First in the pipeline, so everything after it sees the client's own address and scheme.</summary>
    public static IApplicationBuilder UseOpenPortalReverseProxy(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.ApplicationServices.GetRequiredService<ReverseProxyOptions>().Enabled
            ? app.UseForwardedHeaders()
            : app;
    }
}
