using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenPortal.Access.Infrastructure.Persistence;
using OpenPortal.Audit.Infrastructure.Persistence;
using OpenPortal.Content.Infrastructure.Persistence;
using OpenPortal.Identity.Infrastructure.Persistence;

namespace OpenPortal.Web.Health;

/// <summary>
/// Health endpoints for a load balancer, a container orchestrator or an uptime monitor.
/// <list type="bullet">
/// <item><c>/health/live</c>: the process answers. Checks nothing else, so a database outage does not get the
/// container restarted in a loop.</item>
/// <item><c>/health/ready</c>: every module's database is reachable (503 when one is not) and its migrations are
/// applied (<c>Degraded</c>, still 200, when some are pending: the portal runs, but a deployment step was
/// skipped).</item>
/// </list>
/// Both are anonymous and name only the checks and their state, never an exception or a connection string.
/// </summary>
public static class HealthSetup
{
    public const string LivePath = "/health/live";
    public const string ReadyPath = "/health/ready";

    private const string ReadyTag = "ready";

    public static IServiceCollection AddOpenPortalHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck<IdentityDbContext>>("database-identity", tags: [ReadyTag])
            .AddCheck<DatabaseHealthCheck<ContentDbContext>>("database-content", tags: [ReadyTag])
            .AddCheck<DatabaseHealthCheck<AccessDbContext>>("database-access", tags: [ReadyTag])
            .AddCheck<DatabaseHealthCheck<AuditDbContext>>("database-audit", tags: [ReadyTag]);

        return services;
    }

    public static IEndpointRouteBuilder MapOpenPortalHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks(LivePath, new HealthCheckOptions
            {
                Predicate = _ => false,
                ResponseWriter = WriteAsync,
            })
            .DisableRateLimiting();

        endpoints.MapHealthChecks(ReadyPath, new HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains(ReadyTag),
                ResponseWriter = WriteAsync,
            })
            .DisableRateLimiting();

        return endpoints;
    }

    private static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store";

        var payload = new
        {
            status = report.Status.ToString(),
            durationMs = Math.Round(report.TotalDuration.TotalMilliseconds),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                durationMs = Math.Round(entry.Value.Duration.TotalMilliseconds),
            }),
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonSerializerOptions.Web));
    }
}

/// <summary>
/// Whether one module's database answers, and whether its schema is up to date. Its own scope, so it never
/// shares a context with a request.
/// </summary>
internal sealed class DatabaseHealthCheck<TContext> : IHealthCheck
    where TContext : DbContext
{
    private readonly IServiceScopeFactory _scopes;

    public DatabaseHealthCheck(IServiceScopeFactory scopes)
    {
        _scopes = scopes;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();

        try
        {
            if (!await db.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false))
            {
                return HealthCheckResult.Unhealthy("The database cannot be reached.");
            }

            var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).Count();

            return pending == 0
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Degraded($"{pending} migration(s) not applied.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The exception is logged by the health check service; the response only says what failed.
            return HealthCheckResult.Unhealthy("The database check failed.", exception);
        }
    }
}
