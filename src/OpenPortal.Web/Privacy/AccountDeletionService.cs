using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.SharedKernel.Time;
using OpenPortal.Web.Users;

namespace OpenPortal.Web.Privacy;

/// <summary>
/// Deletes the accounts whose owners asked for it once their grace period is over, shortly after startup and then
/// every hour. Several instances may run it at once: an account already gone is simply not found again.
/// </summary>
public sealed partial class AccountDeletionService : BackgroundService
{
    private static readonly TimeSpan FirstRunDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopes;
    private readonly IClock _clock;
    private readonly ILogger<AccountDeletionService> _logger;

    public AccountDeletionService(IServiceScopeFactory scopes, IClock clock, ILogger<AccountDeletionService> logger)
    {
        _scopes = scopes;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Deletes every account due now and returns how many went.</summary>
    public async Task<int> SweepAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var erasure = scope.ServiceProvider.GetRequiredService<IAccountErasure>();
        var eraser = scope.ServiceProvider.GetRequiredService<AccountEraser>();

        var deleted = 0;
        foreach (var userId in await erasure.DueForDeletionAsync(_clock.UtcNow, cancellationToken).ConfigureAwait(false))
        {
            var erased = await eraser.EraseScheduledAsync(userId, cancellationToken).ConfigureAwait(false);
            if (erased.IsSuccess)
            {
                deleted++;
            }
            else
            {
                LogEraseFailed(userId, erased.Error.Code);
            }
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
                    var deleted = await SweepAsync(stoppingToken).ConfigureAwait(false);
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted {Count} accounts whose owners asked for it.")]
    private partial void LogSwept(int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The scheduled deletion of account {UserId} failed ({ErrorCode}); it will be tried again.")]
    private partial void LogEraseFailed(Guid userId, string errorCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The scheduled account deletion sweep failed; it will run again later.")]
    private partial void LogSweepFailed(Exception exception);
}
