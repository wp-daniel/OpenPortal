using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenPortal.Notifications.Infrastructure.Configuration;
using OpenPortal.Notifications.Application.Abstractions;
using OpenPortal.Notifications.Application.Auditing;
using OpenPortal.Notifications.Application.Contracts;
using OpenPortal.Notifications.Domain;
using OpenPortal.Notifications.Infrastructure.Persistence;
using OpenPortal.SharedKernel.Auditing;
using OpenPortal.SharedKernel.Results;
using OpenPortal.SharedKernel.Time;

namespace OpenPortal.Notifications.Infrastructure.Services;

/// <inheritdoc />
internal sealed class OutboxAdministration : IOutboxAdministration
{
    private readonly NotificationsDbContext _db;
    private readonly IDeliveryAdministrationAuthorization _authorization;
    private readonly OutboxSignal _signal;
    private readonly OutboxProcessor _processor;
    private readonly IOptions<OutboxOptions> _options;
    private readonly IClock _clock;
    private readonly IAuditTrail _audit;

    public OutboxAdministration(
        NotificationsDbContext db,
        IDeliveryAdministrationAuthorization authorization,
        OutboxSignal signal,
        OutboxProcessor processor,
        IOptions<OutboxOptions> options,
        IClock clock,
        IAuditTrail audit)
    {
        _db = db;
        _authorization = authorization;
        _signal = signal;
        _processor = processor;
        _options = options;
        _clock = clock;
        _audit = audit;
    }

    public async Task<Result<OutboxPageDto>> ListAsync(OutboxQuery query, CancellationToken cancellationToken)
    {
        var guard = await _authorization.EnsureCanManageAsync(cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<OutboxPageDto>.Failure(guard.Error);
        }

        ArgumentNullException.ThrowIfNull(query);

        var messages = _db.OutboxMessages.AsNoTracking();

        if (TryParseStatus(query.Status, out var status))
        {
            messages = messages.Where(message => message.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Type))
        {
            var type = query.Type.Trim();
            messages = messages.Where(message => message.Type == type);
        }

        var total = await messages.CountAsync(cancellationToken).ConfigureAwait(false);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var page = Math.Max(1, query.Page);

        var items = await messages
            .OrderByDescending(message => message.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var now = _clock.UtcNow;
        var dayAgo = now.AddDays(-1);

        var pending = await _db.OutboxMessages.CountAsync(message => message.Status == OutboxMessageStatus.Pending, cancellationToken).ConfigureAwait(false);
        var failed = await _db.OutboxMessages.CountAsync(message => message.Status == OutboxMessageStatus.Failed, cancellationToken).ConfigureAwait(false);
        var delivered = await _db.OutboxMessages
            .CountAsync(message => message.Status == OutboxMessageStatus.Delivered && message.DeliveredAtUtc >= dayAgo, cancellationToken)
            .ConfigureAwait(false);
        var oldest = await _db.OutboxMessages
            .Where(message => message.Status == OutboxMessageStatus.Pending)
            .OrderBy(message => message.CreatedAtUtc)
            .Select(message => (DateTimeOffset?)message.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result<OutboxPageDto>.Success(new OutboxPageDto(
            new OutboxSummaryDto(pending, failed, delivered, oldest),
            items.Select(ToDto).ToList(),
            total));
    }

    public async Task<Result<OutboxMessageDto>> RetryAsync(Guid messageId, CancellationToken cancellationToken)
    {
        var guard = await _authorization.EnsureCanManageAsync(cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<OutboxMessageDto>.Failure(guard.Error);
        }

        var message = await _db.OutboxMessages.FirstOrDefaultAsync(row => row.Id == messageId, cancellationToken).ConfigureAwait(false);
        if (message is null)
        {
            return Result<OutboxMessageDto>.Failure(OutboxErrors.NotFound);
        }

        var retried = message.Retry(_clock.UtcNow);
        if (retried.IsFailure)
        {
            return Result<OutboxMessageDto>.Failure(retried.Error);
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _audit.RecordAsync(
                AuditEvent.Succeeded(NotificationAuditActions.OutboxRetried, Subject(message)),
                cancellationToken)
            .ConfigureAwait(false);

        await DispatchAsync([message.Id], cancellationToken).ConfigureAwait(false);

        var current = await _db.OutboxMessages.AsNoTracking().FirstAsync(row => row.Id == messageId, cancellationToken).ConfigureAwait(false);

        return Result<OutboxMessageDto>.Success(ToDto(current));
    }

    public async Task<Result<int>> RetryFailedAsync(CancellationToken cancellationToken)
    {
        var guard = await _authorization.EnsureCanManageAsync(cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<int>.Failure(guard.Error);
        }

        var failed = await _db.OutboxMessages
            .Where(message => message.Status == OutboxMessageStatus.Failed)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var now = _clock.UtcNow;
        foreach (var message in failed)
        {
            message.Retry(now);
        }

        if (failed.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await DispatchAsync(failed.Select(message => message.Id).ToList(), cancellationToken).ConfigureAwait(false);

            await _audit.RecordAsync(
                    AuditEvent.Succeeded(
                        NotificationAuditActions.OutboxRetried,
                        details: new Dictionary<string, string?> { ["count"] = failed.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) }),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return Result<int>.Success(failed.Count);
    }

    public async Task<Result> DeleteAsync(Guid messageId, CancellationToken cancellationToken)
    {
        var guard = await _authorization.EnsureCanManageAsync(cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return guard;
        }

        var message = await _db.OutboxMessages.FirstOrDefaultAsync(row => row.Id == messageId, cancellationToken).ConfigureAwait(false);
        if (message is null)
        {
            return Result.Failure(OutboxErrors.NotFound);
        }

        _db.OutboxMessages.Remove(message);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _audit.RecordAsync(AuditEvent.Succeeded(NotificationAuditActions.OutboxDeleted, Subject(message)), cancellationToken)
            .ConfigureAwait(false);

        return Result.Success();
    }

    /// <summary>Wakes the dispatcher, or delivers right here when the queue runs inline.</summary>
    private async Task DispatchAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        if (!_options.Value.DispatchInline)
        {
            _signal.Notify();
            return;
        }

        foreach (var id in ids)
        {
            await _processor.ProcessAsync(id, cancellationToken).ConfigureAwait(false);
        }
    }

    private static AuditSubject Subject(OutboxMessage message) =>
        new("outbox_message", message.Id.ToString(), message.Description ?? message.Type);

    private static bool TryParseStatus(string? value, out OutboxMessageStatus status)
    {
        switch (value?.Trim().ToUpperInvariant())
        {
            case "PENDING":
                status = OutboxMessageStatus.Pending;
                return true;
            case "FAILED":
                status = OutboxMessageStatus.Failed;
                return true;
            case "DELIVERED":
                status = OutboxMessageStatus.Delivered;
                return true;
            default:
                status = default;
                return false;
        }
    }

    private static OutboxMessageDto ToDto(OutboxMessage message) => new(
        message.Id,
        message.Type,
        message.Description,
        message.Status.ToString().ToLowerInvariant(),
        message.Attempts,
        message.CreatedAtUtc,
        message.AvailableAtUtc,
        message.LastAttemptAtUtc,
        message.DeliveredAtUtc,
        message.LastError);
}

/// <inheritdoc />
internal sealed class OutboxMonitor : IOutboxMonitor
{
    private readonly NotificationsDbContext _db;
    private readonly IClock _clock;

    public OutboxMonitor(NotificationsDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<OutboxSummaryDto> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var dayAgo = _clock.UtcNow.AddDays(-1);

        var pending = await _db.OutboxMessages.CountAsync(message => message.Status == OutboxMessageStatus.Pending, cancellationToken).ConfigureAwait(false);
        var failed = await _db.OutboxMessages.CountAsync(message => message.Status == OutboxMessageStatus.Failed, cancellationToken).ConfigureAwait(false);
        var delivered = await _db.OutboxMessages
            .CountAsync(message => message.Status == OutboxMessageStatus.Delivered && message.DeliveredAtUtc >= dayAgo, cancellationToken)
            .ConfigureAwait(false);
        var oldest = await _db.OutboxMessages
            .Where(message => message.Status == OutboxMessageStatus.Pending)
            .OrderBy(message => message.CreatedAtUtc)
            .Select(message => (DateTimeOffset?)message.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return new OutboxSummaryDto(pending, failed, delivered, oldest);
    }
}
