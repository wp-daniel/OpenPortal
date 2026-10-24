using Microsoft.AspNetCore.DataProtection;
using StackExchange.Redis;

namespace OpenPortal.Web.Scale;

/// <summary>Bound from the <c>Redis</c> section.</summary>
public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    /// <summary>
    /// A StackExchange.Redis connection string (<c>redis:6379,password=…,ssl=true</c>). Set it when the portal runs on
    /// several instances: the rate limits, the cache, the real-time notification signals and (with
    /// <see cref="DataProtectionKeys"/>) the session keys are then shared instead of per instance. Empty: everything
    /// stays in the process, which is right for one instance.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>Prefix of every key the portal writes, so one Redis can serve several portals.</summary>
    public string KeyPrefix { get; set; } = "openportal:";

    /// <summary>
    /// Keep the Data Protection keys (session cookies, antiforgery, OpenID Connect state) in Redis instead of the
    /// <c>DataProtection:KeysPath</c> folder, so instances need no shared disk. Switching loses the existing keys:
    /// everyone signs in again once.
    /// </summary>
    public bool DataProtectionKeys { get; set; }
}

/// <summary>
/// The optional Redis connection behind horizontal scaling. Without it every instance keeps its own rate limits,
/// cache and notification signals, which is correct for a single instance and only approximate for several.
/// </summary>
public static class RedisSetup
{
    /// <summary>Registers the connection (opened on first use), the distributed cache and, if asked, the key ring.</summary>
    public static RedisOptions AddOpenPortalRedis(this IServiceCollection services, IConfiguration configuration, IDataProtectionBuilder dataProtection)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(dataProtection);

        var options = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>() ?? new RedisOptions();
        services.AddSingleton(options);

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            services.AddDistributedMemoryCache();
            return options;
        }

        var connection = new Lazy<IConnectionMultiplexer>(() => ConnectionMultiplexer.Connect(options.ConnectionString));
        services.AddSingleton<IConnectionMultiplexer>(_ => connection.Value);

        services.AddStackExchangeRedisCache(cache =>
        {
            cache.ConnectionMultiplexerFactory = () => Task.FromResult(connection.Value);
            cache.InstanceName = options.KeyPrefix + "cache:";
        });

        if (options.DataProtectionKeys)
        {
            dataProtection.PersistKeysToStackExchangeRedis(() => connection.Value.GetDatabase(), options.KeyPrefix + "dataprotection-keys");
        }

        services.AddHealthChecks().AddCheck<RedisHealthCheck>("redis", tags: ["ready"]);

        return options;
    }
}

/// <summary>Redis answers a PING. Unhealthy (503 on /health/ready) when it does not, since instances then drift apart.</summary>
internal sealed class RedisHealthCheck : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    private readonly IConnectionMultiplexer _redis;

    public RedisHealthCheck(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var latency = await _redis.GetDatabase().PingAsync().ConfigureAwait(false);
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy($"{latency.TotalMilliseconds:0} ms");
        }
        catch (RedisException)
        {
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("Redis does not answer.");
        }
    }
}
