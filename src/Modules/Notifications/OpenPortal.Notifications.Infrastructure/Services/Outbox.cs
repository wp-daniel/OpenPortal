using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenPortal.Notifications.Domain;
using OpenPortal.Notifications.Infrastructure.Configuration;
using OpenPortal.Notifications.Infrastructure.Persistence;
using OpenPortal.SharedKernel.Messaging;
using OpenPortal.SharedKernel.Time;

namespace OpenPortal.Notifications.Infrastructure.Services;

/// <summary>Wakes this instance's dispatcher as soon as something is queued, instead of at the next poll.</summary>
internal sealed class OutboxSignal
{
    private readonly SemaphoreSlim _signal = new(0, 1);

    public void Notify()
    {
        if (_signal.CurrentCount == 0)
        {
            try
            {
                _signal.Release();
            }
            catch (SemaphoreFullException)
            {
                // Already signalled.
            }
        }
    }

    public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken) =>
        _signal.WaitAsync(timeout, cancellationToken);
}

/// <summary>The counters exported to OpenTelemetry (meter <c>OpenPortal.Outbox</c>).</summary>
internal static class OutboxMetrics
{
    public const string MeterName = "OpenPortal.Outbox";

    private static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> Enqueued = Meter.CreateCounter<long>("openportal.outbox.enqueued", description: "Messages queued.");

    public static readonly Counter<long> Delivered = Meter.CreateCounter<long>("openportal.outbox.delivered", description: "Messages delivered.");

    public static readonly Counter<long> AttemptsFailed = Meter.CreateCounter<long>("openportal.outbox.attempts_failed", description: "Delivery attempts that failed.");

    public static readonly Counter<long> GaveUp = Meter.CreateCounter<long>("openportal.outbox.failed", description: "Messages marked failed.");

    public static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("openportal.outbox.delivery.duration", unit: "ms", description: "Time one delivery attempt took.");
}

/// <summary>Writes messages to the queue in a context of its own, so a request's unsaved changes never ride along.</summary>
internal sealed class Outbox : IOutbox
{
    private readonly IServiceScopeFactory _scopes;
    private readonly OutboxSignal _signal;
    private readonly OutboxProcessor _processor;
    private readonly IOptions<OutboxOptions> _options;
    private readonly IClock _clock;

    public Outbox(IServiceScopeFactory scopes, OutboxSignal signal, OutboxProcessor processor, IOptions<OutboxOptions> options, IClock clock)
    {
        _scopes = scopes;
        _signal = signal;
        _processor = processor;
        _options = options;
        _clock = clock;
    }

    public Task EnqueueAsync(OutboxMessageDraft message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        return EnqueueAsync([message], cancellationToken);
    }

    public async Task EnqueueAsync(IReadOnlyCollection<OutboxMessageDraft> messages, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(messages);

        if (messages.Count == 0)
        {
            return;
        }

        var now = _clock.UtcNow;
        var rows = new List<OutboxMessage>(messages.Count);

        foreach (var draft in messages)
        {
            var created = OutboxMessage.Create(Guid.NewGuid(), draft.Type, draft.Payload, draft.Description, now);
            if (created.IsFailure)
            {
                // A caller bug, not a delivery problem: fail loudly in development and tests.
                throw new ArgumentException(created.Error.Description, nameof(messages));
            }

            rows.Add(created.Value);
        }

        await using (var scope = _scopes.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
            db.OutboxMessages.AddRange(rows);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (var row in rows)
        {
            OutboxMetrics.Enqueued.Add(1, new KeyValuePair<string, object?>("type", row.Type));
        }

        if (_options.Value.DispatchInline)
        {
            foreach (var row in rows)
            {
                await _processor.ProcessAsync(row.Id, cancellationToken).ConfigureAwait(false);
            }

            return;
        }

        _signal.Notify();
    }
}

/// <summary>
/// Claims due messages with a lease and hands each to the handler of its type. Safe to run on several instances at
/// once: a claim is a conditional update that only one of them can win.
/// </summary>
internal sealed partial class OutboxProcessor
{
    private static readonly string Owner = InstanceName();

    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<OutboxOptions> _options;
    private readonly IClock _clock;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(IServiceScopeFactory scopes, IOptions<OutboxOptions> options, IClock clock, ILogger<OutboxProcessor> logger)
    {
        _scopes = scopes;
        _options = options;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Delivers what is due now, one batch, and returns how many messages it attempted.</summary>
    public async Task<int> ProcessDueAsync(CancellationToken cancellationToken)
    {
        var options = _options.Value;
        var now = _clock.UtcNow;

        List<Guid> due;
        await using (var scope = _scopes.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();

            due = await db.OutboxMessages
                .AsNoTracking()
                .Where(message => message.Status == OutboxMessageStatus.Pending
                    && message.AvailableAtUtc <= now
                    && (message.LockedUntilUtc == null || message.LockedUntilUtc < now))
                .OrderBy(message => message.AvailableAtUtc)
                .Select(message => message.Id)
                .Take(Math.Max(1, options.BatchSize))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        var attempted = 0;
        foreach (var id in due)
        {
            if (await ProcessAsync(id, cancellationToken).ConfigureAwait(false))
            {
                attempted++;
            }
        }

        return attempted;
    }

    /// <summary>Claims and delivers one message; false when it was not due or another instance holds it.</summary>
    public async Task<bool> ProcessAsync(Guid messageId, CancellationToken cancellationToken)
    {
        var options = _options.Value;
        var now = _clock.UtcNow;
        var leaseUntil = now.AddSeconds(Math.Max(10, options.LeaseSeconds));

        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();

        var claimed = await db.OutboxMessages
            .Where(message => message.Id == messageId
                && message.Status == OutboxMessageStatus.Pending
                && message.AvailableAtUtc <= now
                && (message.LockedUntilUtc == null || message.LockedUntilUtc < now))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(message => message.LockedUntilUtc, leaseUntil)
                    .SetProperty(message => message.LockedBy, Owner),
                cancellationToken)
            .ConfigureAwait(false);

        if (claimed == 0)
        {
            return false;
        }

        var row = await db.OutboxMessages.FirstAsync(message => message.Id == messageId, cancellationToken).ConfigureAwait(false);
        var handler = scope.ServiceProvider.GetServices<IOutboxHandler>()
            .FirstOrDefault(candidate => string.Equals(candidate.Type, row.Type, StringComparison.Ordinal));

        var type = new KeyValuePair<string, object?>("type", row.Type);
        var watch = Stopwatch.StartNew();

        try
        {
            if (handler is null)
            {
                throw new OutboxPermanentFailureException($"No handler is registered for messages of type '{row.Type}'.");
            }

            await handler.HandleAsync(row.Payload, cancellationToken).ConfigureAwait(false);

            row.MarkDelivered(_clock.UtcNow);
            OutboxMetrics.Delivered.Add(1, type);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutting down: the lease runs out and another attempt picks it up.
            throw;
        }
        catch (Exception exception)
        {
            var permanent = exception is OutboxPermanentFailureException;
            row.MarkAttemptFailed(exception.GetBaseException().Message, _clock.UtcNow, Math.Max(1, options.MaxAttempts), permanent);

            OutboxMetrics.AttemptsFailed.Add(1, type);
            if (row.Status == OutboxMessageStatus.Failed)
            {
                OutboxMetrics.GaveUp.Add(1, type);
                LogGaveUp(exception, row.Type, row.Id, row.Attempts);
            }
            else
            {
                LogAttemptFailed(exception, row.Type, row.Id, row.Attempts, row.AvailableAtUtc);
            }
        }
        finally
        {
            OutboxMetrics.Duration.Record(watch.Elapsed.TotalMilliseconds, type);
        }

        // The outcome is saved even when the caller gave up waiting: the work was done (or tried).
        await db.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);

        return true;
    }

    /// <summary>Deletes delivered and failed messages past their retention; returns how many went.</summary>
    public async Task<int> SweepAsync(CancellationToken cancellationToken)
    {
        var options = _options.Value;
        var now = _clock.UtcNow;
        var deleted = 0;

        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();

        if (options.DeliveredRetentionDays > 0)
        {
            var before = now.AddDays(-options.DeliveredRetentionDays);
            deleted += await db.OutboxMessages
                .Where(message => message.Status == OutboxMessageStatus.Delivered && message.CreatedAtUtc < before)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        if (options.FailedRetentionDays > 0)
        {
            var before = now.AddDays(-options.FailedRetentionDays);
            deleted += await db.OutboxMessages
                .Where(message => message.Status == OutboxMessageStatus.Failed && message.CreatedAtUtc < before)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        return deleted;
    }

    private static string InstanceName()
    {
        var name = $"{Environment.MachineName}:{Environment.ProcessId}";
        return name.Length > OutboxMessage.OwnerMaxLength ? name[..OutboxMessage.OwnerMaxLength] : name;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Delivery of {Type} message {Id} failed (attempt {Attempts}); next attempt at {NextAttempt}.")]
    private partial void LogAttemptFailed(Exception exception, string type, Guid id, int attempts, DateTimeOffset nextAttempt);

    [LoggerMessage(Level = LogLevel.Error, Message = "Gave up delivering {Type} message {Id} after {Attempts} attempts.")]
    private partial void LogGaveUp(Exception exception, string type, Guid id, int attempts);
}

/// <summary>Runs the queue on this instance: due messages as soon as they are signalled or at each poll, and an hourly sweep.</summary>
internal sealed partial class OutboxDispatcher : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromHours(1);

    private readonly OutboxProcessor _processor;
    private readonly OutboxSignal _signal;
    private readonly IOptions<OutboxOptions> _options;
    private readonly ILogger<OutboxDispatcher> _logger;

    public OutboxDispatcher(OutboxProcessor processor, OutboxSignal signal, IOptions<OutboxOptions> options, ILogger<OutboxDispatcher> logger)
    {
        _processor = processor;
        _signal = signal;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.Value.DispatchInline)
        {
            // Every message is delivered by the request that queued (or retried) it.
            return;
        }

        var poll = TimeSpan.FromSeconds(Math.Max(1, _options.Value.PollSeconds));
        var nextSweep = DateTimeOffset.UtcNow.AddMinutes(5);

        try
        {
            // Let the host finish starting (and the database be migrated) first.
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken).ConfigureAwait(false);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Keep going while full batches come back: a backlog drains without waiting for the next poll.
                    while (await _processor.ProcessDueAsync(stoppingToken).ConfigureAwait(false) >= _options.Value.BatchSize)
                    {
                    }

                    if (DateTimeOffset.UtcNow >= nextSweep)
                    {
                        nextSweep = DateTimeOffset.UtcNow + SweepInterval;
                        var swept = await _processor.SweepAsync(stoppingToken).ConfigureAwait(false);
                        if (swept > 0)
                        {
                            LogSwept(swept);
                        }
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    // A database that is briefly unavailable must not stop the host.
                    LogPassFailed(exception);
                }

                await _signal.WaitAsync(poll, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted {Count} old messages from the delivery queue.")]
    private partial void LogSwept(int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "A pass of the delivery queue failed; it will run again shortly.")]
    private partial void LogPassFailed(Exception exception);
}
