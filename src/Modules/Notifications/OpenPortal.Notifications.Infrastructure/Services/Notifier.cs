using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenPortal.Notifications.Application.Abstractions;
using OpenPortal.Notifications.Application.Contracts;
using OpenPortal.Notifications.Domain;
using OpenPortal.Notifications.Infrastructure.Configuration;
using OpenPortal.Notifications.Infrastructure.Persistence;
using OpenPortal.SharedKernel.Notifications;
using OpenPortal.SharedKernel.Time;

namespace OpenPortal.Notifications.Infrastructure.Services;

/// <summary>
/// Writes one notification per recipient of a notice: the users it names and the people the host says its roles
/// cover, less the caller who caused it.
/// <para>
/// It has a context of its own, so notifying never flushes or disturbs the changes another module is tracking.
/// A failed write is logged and swallowed, as for the audit trail: the change it announces is already committed.
/// </para>
/// </summary>
internal sealed partial class Notifier : INotifier
{
    private readonly NotificationsDbContext _db;
    private readonly INotificationContext _context;
    private readonly IOptions<NotificationsModuleOptions> _options;
    private readonly IClock _clock;
    private readonly ILogger<Notifier> _logger;
    private readonly INotificationPublisher _publisher;
    private readonly NotificationEmailer _emailer;

    public Notifier(
        NotificationsDbContext db,
        INotificationContext context,
        IOptions<NotificationsModuleOptions> options,
        IClock clock,
        ILogger<Notifier> logger,
        INotificationPublisher publisher,
        NotificationEmailer emailer)
    {
        _emailer = emailer;
        _publisher = publisher;
        _db = db;
        _context = context;
        _options = options;
        _clock = clock;
        _logger = logger;
    }

    public async Task NotifyAsync(Notice notice, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notice);

        try
        {
            var recipients = new HashSet<Guid>(notice.Audience.UserIds);

            foreach (var role in notice.Audience.Roles.Distinct(StringComparer.Ordinal))
            {
                recipients.UnionWith(await _context.ResolveRoleAsync(role, cancellationToken).ConfigureAwait(false));
            }

            // Nobody needs to be told about what they just did themselves.
            if (!notice.IncludeCaller && _context.CurrentUserId is Guid caller)
            {
                recipients.Remove(caller);
            }

            recipients.Remove(Guid.Empty);

            if (recipients.Count == 0)
            {
                return;
            }

            var parameters = notice.Parameters is { Count: > 0 }
                ? JsonSerializer.Serialize(notice.Parameters)
                : null;

            var now = _clock.UtcNow;
            var limit = Math.Max(1, _options.Value.MaxRecipientsPerNotice);

            var reached = recipients.Order().Take(limit).ToList();

            foreach (var recipient in reached)
            {
                var notification = Notification.Create(Guid.NewGuid(), recipient, notice.Type, parameters, notice.Link, now);
                if (notification.IsFailure)
                {
                    // A malformed type is a programming error in the sender; one log line says so.
                    LogNoticeRejected(notice.Type, notification.Error.Code);
                    return;
                }

                _db.Notifications.Add(notification.Value);
            }

            if (recipients.Count > limit)
            {
                LogRecipientsCapped(notice.Type, recipients.Count, limit);
            }

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _db.ChangeTracker.Clear();

            await _publisher.PublishAsync(reached, cancellationToken).ConfigureAwait(false);

            await _emailer.SendAsync(
                    reached,
                    new NotificationMailItem(
                        notice.Type,
                        notice.Parameters ?? new Dictionary<string, string?>(),
                        Notification.NormaliseLink(notice.Link),
                        now),
                    notice.Security,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031 // Notifying is best effort by design; see the class remarks.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            _db.ChangeTracker.Clear();
            LogNotifyFailed(exception, notice.Type);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not record the notification {Type}.")]
    private partial void LogNotifyFailed(Exception exception, string type);

    [LoggerMessage(Level = LogLevel.Error, Message = "The notification {Type} was not sent: {Reason}.")]
    private partial void LogNoticeRejected(string type, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The notification {Type} named {Count} recipients; only the first {Limit} received it.")]
    private partial void LogRecipientsCapped(string type, int count, int limit);
}
