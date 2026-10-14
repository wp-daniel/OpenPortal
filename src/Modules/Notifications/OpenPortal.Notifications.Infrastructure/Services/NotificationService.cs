using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpenPortal.Notifications.Application.Abstractions;
using OpenPortal.Notifications.Application.Contracts;
using OpenPortal.Notifications.Domain;
using OpenPortal.Notifications.Infrastructure.Persistence;
using OpenPortal.SharedKernel.Results;
using OpenPortal.SharedKernel.Time;

namespace OpenPortal.Notifications.Infrastructure.Services;

/// <summary>The signed-in user's notification center: always and only their own notifications.</summary>
internal sealed class NotificationService : INotificationService
{
    private static readonly IReadOnlyDictionary<string, string?> NoParameters = new Dictionary<string, string?>();

    private readonly NotificationsDbContext _db;
    private readonly INotificationContext _context;
    private readonly IClock _clock;

    public NotificationService(NotificationsDbContext db, INotificationContext context, IClock clock)
    {
        _db = db;
        _context = context;
        _clock = clock;
    }

    public async Task<Result<NotificationPageDto>> ListAsync(NotificationQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (_context.CurrentUserId is not Guid userId)
        {
            return Result<NotificationPageDto>.Failure(NotificationErrors.SignInRequired);
        }

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, NotificationQuery.MaxPageSize);

        var mine = Mine(userId);
        var filtered = query.UnreadOnly ? mine.Where(notification => notification.ReadAtUtc == null) : mine;

        var totalCount = await filtered.CountAsync(cancellationToken).ConfigureAwait(false);
        var unreadCount = query.UnreadOnly
            ? totalCount
            : await mine.CountAsync(notification => notification.ReadAtUtc == null, cancellationToken).ConfigureAwait(false);

        // The id breaks ties between notifications written in the same tick, so paging is stable.
        var items = await filtered
            .OrderByDescending(notification => notification.CreatedAtUtc)
            .ThenByDescending(notification => notification.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result<NotificationPageDto>.Success(
            new NotificationPageDto(items.Select(ToDto).ToList(), page, pageSize, totalCount, unreadCount));
    }

    public async Task<Result<NotificationCountDto>> CountAsync(CancellationToken cancellationToken)
    {
        if (_context.CurrentUserId is not Guid userId)
        {
            return Result<NotificationCountDto>.Failure(NotificationErrors.SignInRequired);
        }

        var unread = await Mine(userId)
            .CountAsync(notification => notification.ReadAtUtc == null, cancellationToken)
            .ConfigureAwait(false);

        var latest = await Mine(userId)
            .OrderByDescending(notification => notification.CreatedAtUtc)
            .Select(notification => (DateTimeOffset?)notification.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result<NotificationCountDto>.Success(new NotificationCountDto(unread, latest));
    }

    public async Task<Result<NotificationDto>> SetReadAsync(Guid notificationId, bool read, CancellationToken cancellationToken)
    {
        if (_context.CurrentUserId is not Guid userId)
        {
            return Result<NotificationDto>.Failure(NotificationErrors.SignInRequired);
        }

        var notification = await _db.Notifications
            .SingleOrDefaultAsync(candidate => candidate.Id == notificationId && candidate.RecipientId == userId, cancellationToken)
            .ConfigureAwait(false);

        if (notification is null)
        {
            return Result<NotificationDto>.Failure(NotificationErrors.NotFound);
        }

        var changed = read ? notification.MarkRead(_clock.UtcNow) : notification.MarkUnread();
        if (changed)
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return Result<NotificationDto>.Success(ToDto(notification));
    }

    public async Task<Result<int>> MarkAllReadAsync(CancellationToken cancellationToken)
    {
        if (_context.CurrentUserId is not Guid userId)
        {
            return Result<int>.Failure(NotificationErrors.SignInRequired);
        }

        var now = _clock.UtcNow;

        var changed = await _db.Notifications
            .Where(notification => notification.RecipientId == userId && notification.ReadAtUtc == null)
            .ExecuteUpdateAsync(set => set.SetProperty(notification => notification.ReadAtUtc, now), cancellationToken)
            .ConfigureAwait(false);

        return Result<int>.Success(changed);
    }

    public async Task<Result> DeleteAsync(Guid notificationId, CancellationToken cancellationToken)
    {
        if (_context.CurrentUserId is not Guid userId)
        {
            return Result.Failure(NotificationErrors.SignInRequired);
        }

        var deleted = await _db.Notifications
            .Where(notification => notification.RecipientId == userId && notification.Id == notificationId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        return deleted == 0 ? Result.Failure(NotificationErrors.NotFound) : Result.Success();
    }

    public async Task<Result<int>> DeleteReadAsync(CancellationToken cancellationToken)
    {
        if (_context.CurrentUserId is not Guid userId)
        {
            return Result<int>.Failure(NotificationErrors.SignInRequired);
        }

        var deleted = await _db.Notifications
            .Where(notification => notification.RecipientId == userId && notification.ReadAtUtc != null)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result<int>.Success(deleted);
    }

    private IQueryable<Notification> Mine(Guid userId) =>
        _db.Notifications.AsNoTracking().Where(notification => notification.RecipientId == userId);

    private static NotificationDto ToDto(Notification notification) => new(
        notification.Id,
        notification.Type,
        ReadParameters(notification.Parameters),
        notification.Link,
        notification.CreatedAtUtc,
        notification.ReadAtUtc);

    private static IReadOnlyDictionary<string, string?> ReadParameters(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return NoParameters;
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string?>>(json) ?? NoParameters;
        }
        catch (JsonException)
        {
            // Written by this module only, so this means a hand-edited row: show the notice without its values.
            return NoParameters;
        }
    }
}

/// <inheritdoc />
internal sealed class NotificationMaintenanceService : INotificationMaintenance
{
    private readonly NotificationsDbContext _db;

    public NotificationMaintenanceService(NotificationsDbContext db)
    {
        _db = db;
    }

    public Task ForgetRecipientAsync(Guid userId, CancellationToken cancellationToken) =>
        _db.Notifications
            .Where(notification => notification.RecipientId == userId)
            .ExecuteDeleteAsync(cancellationToken);
}
