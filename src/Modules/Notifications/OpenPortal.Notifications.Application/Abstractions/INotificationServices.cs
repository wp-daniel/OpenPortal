using OpenPortal.Notifications.Application.Contracts;
using OpenPortal.SharedKernel.Notifications;
using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Notifications.Application.Abstractions;

/// <summary>
/// The signed-in user's notification center. Every method works on the caller's own notifications only, which
/// <see cref="INotificationContext"/> identifies; a notification of someone else is reported as not found.
/// Sending goes through the shared kernel's <see cref="INotifier"/>.
/// </summary>
public interface INotificationService
{
    /// <summary>One page of the caller's notifications, newest first.</summary>
    Task<Result<NotificationPageDto>> ListAsync(NotificationQuery query, CancellationToken cancellationToken);

    /// <summary>How many of the caller's notifications are unread.</summary>
    Task<Result<NotificationCountDto>> CountAsync(CancellationToken cancellationToken);

    /// <summary>Marks one read (or unread again). Repeating it changes nothing.</summary>
    Task<Result<NotificationDto>> SetReadAsync(Guid notificationId, bool read, CancellationToken cancellationToken);

    /// <summary>Marks every unread notification read and returns how many changed.</summary>
    Task<Result<int>> MarkAllReadAsync(CancellationToken cancellationToken);

    Task<Result> DeleteAsync(Guid notificationId, CancellationToken cancellationToken);

    /// <summary>Deletes every notification already read and returns how many went.</summary>
    Task<Result<int>> DeleteReadAsync(CancellationToken cancellationToken);
}

/// <summary>The signed-in user's choice of email for their notifications (<see cref="INotificationContext"/>).</summary>
public interface INotificationPreferenceService
{
    Task<Result<NotificationPreferencesDto>> GetMineAsync(CancellationToken cancellationToken);

    Task<Result<NotificationPreferencesDto>> UpdateMineAsync(UpdateNotificationPreferencesRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Emails notifications. Implemented by the host, which knows the recipient's address and language, writes the texts
/// (the same <c>notification.&lt;type&gt;.*</c> the client shows) and queues the email.
/// </summary>
public interface INotificationMailer
{
    Task<bool> CanSendAsync(CancellationToken cancellationToken);

    /// <summary>One notification (<paramref name="digest"/> false), or the day's unread ones in one email.</summary>
    Task SendAsync(Guid recipientId, IReadOnlyList<NotificationMailItem> items, bool digest, CancellationToken cancellationToken);
}

/// <summary>
/// Housekeeping the host performs on behalf of other modules. Performs no caller check: the host calls it after
/// an operation that was authorized already (an account was deleted), so it must not be put behind an endpoint.
/// </summary>
public interface INotificationMaintenance
{
    /// <summary>Forgets every notification of an account that was deleted.</summary>
    Task ForgetRecipientAsync(Guid userId, CancellationToken cancellationToken);
}

/// <summary>
/// Who is calling and who a <see cref="NoticeAudience"/> role names. Supplied by the host, which knows the
/// signed-in user and which portal pages (and so which people) each kind of work needs.
/// </summary>
public interface INotificationContext
{
    /// <summary>The signed-in caller, or null for an anonymous request or a background job.</summary>
    Guid? CurrentUserId { get; }

    /// <summary>The accounts that may do the work <paramref name="role"/> names; none for a role the host does not know.</summary>
    Task<IReadOnlyCollection<Guid>> ResolveRoleAsync(string role, CancellationToken cancellationToken);
}

/// <summary>
/// Tells open browsers that someone's notifications changed, so the bell updates at once instead of at its next poll.
/// Supplied by the host (in-process, or across instances through Redis); the module only says whose.
/// </summary>
public interface INotificationPublisher
{
    Task PublishAsync(IReadOnlyCollection<Guid> recipientIds, CancellationToken cancellationToken);
}
