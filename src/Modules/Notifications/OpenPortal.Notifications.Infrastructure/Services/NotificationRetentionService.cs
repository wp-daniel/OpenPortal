using OpenPortal.SharedKernel.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenPortal.Notifications.Infrastructure.Configuration;
using OpenPortal.Notifications.Infrastructure.Persistence;
using OpenPortal.SharedKernel.Time;

namespace OpenPortal.Notifications.Infrastructure.Services;

/// <summary>
/// Deletes notifications read more than <see cref="NotificationsModuleOptions.ReadRetentionDays"/> ago, and any
/// older than <see cref="NotificationsModuleOptions.RetentionDays"/>, shortly after startup and then once a day.
/// Several instances may run it at once: deleting by age is idempotent.
/// </summary>
internal sealed partial class NotificationRetentionService : BackgroundService
{
    /// <summary>The job's name in the job list (Administration → System).</summary>
    public const string JobName = "notifications.retention";

    private readonly IJobRegistry _jobs;

    private static readonly TimeSpan FirstRunDelay = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<NotificationsModuleOptions> _options;
    private readonly IClock _clock;
    private readonly ILogger<NotificationRetentionService> _logger;

    public NotificationRetentionService(
        IServiceScopeFactory scopes,
        IOptions<NotificationsModuleOptions> options,
        IClock clock,
        ILogger<NotificationRetentionService> logger,
        IJobRegistry jobs)
    {
        _jobs = jobs;
        jobs.Register(JobName, Interval, SweepAsync);
        _scopes = scopes;
        _options = options;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Deletes the expired notifications now and returns how many went.</summary>
    public async Task<int> SweepAsync(CancellationToken cancellationToken)
    {
        var options = _options.Value;
        var now = _clock.UtcNow;
        var deleted = 0;

        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();

        if (options.ReadRetentionDays > 0)
        {
            var readBefore = now.AddDays(-options.ReadRetentionDays);

            deleted += await db.Notifications
                .Where(notification => notification.ReadAtUtc != null && notification.ReadAtUtc < readBefore)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        if (options.RetentionDays > 0)
        {
            var createdBefore = now.AddDays(-options.RetentionDays);

            deleted += await db.Notifications
                .Where(notification => notification.CreatedAtUtc < createdBefore)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        return deleted;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(FirstRunDelay, stoppingToken).ConfigureAwait(false);

            using var timer = new PeriodicTimer(Interval);

            do
            {
                try
                {
                    var deleted = await _jobs.RunAsync(JobName, stoppingToken).ConfigureAwait(false);
                    if (deleted > 0)
                    {
                        LogSwept(deleted);
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted {Count} expired notifications.")]
    private partial void LogSwept(int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The notification retention sweep failed; it will run again later.")]
    private partial void LogSweepFailed(Exception exception);
}
