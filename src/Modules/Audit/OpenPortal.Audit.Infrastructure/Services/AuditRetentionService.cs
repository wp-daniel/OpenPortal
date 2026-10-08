using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenPortal.Audit.Infrastructure.Configuration;
using OpenPortal.Audit.Infrastructure.Persistence;
using OpenPortal.SharedKernel.Time;

namespace OpenPortal.Audit.Infrastructure.Services;

/// <summary>
/// Deletes entries older than <see cref="AuditModuleOptions.RetentionDays"/>, shortly after startup and then
/// once a day. Several instances may run it at once: deleting by age is idempotent.
/// </summary>
internal sealed partial class AuditRetentionService : BackgroundService
{
    private static readonly TimeSpan FirstRunDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<AuditModuleOptions> _options;
    private readonly IClock _clock;
    private readonly ILogger<AuditRetentionService> _logger;

    public AuditRetentionService(
        IServiceScopeFactory scopes,
        IOptions<AuditModuleOptions> options,
        IClock clock,
        ILogger<AuditRetentionService> logger)
    {
        _scopes = scopes;
        _options = options;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Deletes the expired entries now and returns how many went.</summary>
    public async Task<int> SweepAsync(CancellationToken cancellationToken)
    {
        var days = _options.Value.RetentionDays;
        if (days <= 0)
        {
            return 0;
        }

        var cutoff = _clock.UtcNow.AddDays(-days);

        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();

        return await db.Entries
            .Where(entry => entry.OccurredAtUtc < cutoff)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Not at the very start: the host has better things to do while it warms up.
            await Task.Delay(FirstRunDelay, stoppingToken).ConfigureAwait(false);

            using var timer = new PeriodicTimer(Interval);

            do
            {
                try
                {
                    var deleted = await SweepAsync(stoppingToken).ConfigureAwait(false);
                    if (deleted > 0)
                    {
                        LogSwept(deleted, _options.Value.RetentionDays);
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    // A database that is briefly unavailable (or not migrated yet) must not stop the host.
                    LogSweepFailed(exception);
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted {Count} audit entries older than {Days} days.")]
    private partial void LogSwept(int count, int days);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The audit retention sweep failed; it will run again later.")]
    private partial void LogSweepFailed(Exception exception);
}
