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
