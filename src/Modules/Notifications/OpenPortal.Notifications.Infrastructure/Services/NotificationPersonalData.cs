using Microsoft.EntityFrameworkCore;
using OpenPortal.Notifications.Infrastructure.Persistence;
using OpenPortal.SharedKernel.Privacy;

namespace OpenPortal.Notifications.Infrastructure.Services;

/// <summary>The notifications section of a personal data export: every notification the person still has.</summary>
internal sealed class NotificationPersonalData : IPersonalDataSource
{
    private readonly NotificationsDbContext _db;

    public NotificationPersonalData(NotificationsDbContext db)
    {
        _db = db;
    }

    public string Section => "notifications";

    public async Task<object> ExportAsync(Guid userId, CancellationToken cancellationToken)
    {
        var notifications = await _db.Notifications
            .AsNoTracking()
            .Where(notification => notification.RecipientId == userId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return notifications
            .OrderBy(notification => notification.CreatedAtUtc)
            .Select(notification => new
            {
                notification.Type,
                notification.Parameters,
                notification.Link,
                notification.CreatedAtUtc,
                notification.ReadAtUtc,
            })
            .ToList();
    }
}
