using System.Globalization;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenPortal.Notifications.Application.Abstractions;

namespace OpenPortal.Web.Health;

/// <summary>
/// The delivery queue: <c>Degraded</c> (still 200) when messages have failed for good or the oldest waiting one is
/// older than a quarter of an hour, which usually means the mail server or an application is down. Never
/// <c>Unhealthy</c>: the portal itself works.
/// </summary>
internal sealed class OutboxHealthCheck : IHealthCheck
{
    private static readonly TimeSpan Backlog = TimeSpan.FromMinutes(15);

    private readonly IOutboxMonitor _monitor;
    private readonly TimeProvider _time;

    public OutboxHealthCheck(IOutboxMonitor monitor, TimeProvider time)
    {
        _monitor = monitor;
        _time = time;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var summary = await _monitor.GetSummaryAsync(cancellationToken).ConfigureAwait(false);
        var description = string.Create(CultureInfo.InvariantCulture, $"{summary.Pending} waiting, {summary.Failed} failed");

        if (summary.Failed > 0)
        {
            return HealthCheckResult.Degraded(description);
        }

        return summary.OldestPendingAtUtc is { } oldest && _time.GetUtcNow() - oldest > Backlog
            ? HealthCheckResult.Degraded(description + ", backlog")
            : HealthCheckResult.Healthy(description);
    }
}
