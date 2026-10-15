using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Notifications.Application.Abstractions;
using OpenPortal.SharedKernel.Auditing;
using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Web.Users;

/// <summary>
/// Deletes an account and then what the other modules hold about it (notifications, group and organization
/// memberships, grants, access requests, application tokens). The modules never reference each other, so the
/// host composes the steps: the user list, its bulk actions and the scheduled deletion all go through here.
/// </summary>
public sealed class AccountEraser
{
    /// <summary>The actor recorded for a deletion nobody performed by hand: the end of a grace period.</summary>
    public static readonly AuditSubject ScheduledDeletion = new(AuditSubjectTypes.System, "account-deletion", null);

    private readonly IUserAdministrationService _users;
    private readonly IAccountErasure _erasure;
    private readonly IAccessUserMaintenance _access;
    private readonly INotificationMaintenance _notifications;

    public AccountEraser(
        IUserAdministrationService users,
        IAccountErasure erasure,
        IAccessUserMaintenance access,
        INotificationMaintenance notifications)
    {
        _users = users;
        _erasure = erasure;
        _access = access;
        _notifications = notifications;
    }

    /// <summary>An administrator's deletion: checked by the Identity module (page, self, administrators).</summary>
    public async Task<Result> DeleteAsync(Guid userId, CancellationToken cancellationToken)
    {
        var deleted = await _users.DeleteUserAsync(userId, cancellationToken).ConfigureAwait(false);
        if (deleted.IsFailure)
        {
            return deleted;
        }

        await ForgetAsync(userId, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <summary>The deletion an account's owner asked for, carried out once its grace period is over.</summary>
    public async Task<Result> EraseScheduledAsync(Guid userId, CancellationToken cancellationToken)
    {
        var erased = await _erasure.EraseAsync(userId, ScheduledDeletion, cancellationToken).ConfigureAwait(false);
        if (erased.IsFailure)
        {
            return erased;
        }

        await ForgetAsync(userId, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    // The account is gone either way; a failure here leaves only references the screens show as unknown.
    private async Task ForgetAsync(Guid userId, CancellationToken cancellationToken)
    {
        await _notifications.ForgetRecipientAsync(userId, cancellationToken).ConfigureAwait(false);
        await _access.ForgetUserAsync(userId, cancellationToken).ConfigureAwait(false);
    }
}
