using OpenPortal.SharedKernel.Auditing;
using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Identity.Application.Abstractions;

/// <summary>
/// Deletes an account outright, with no caller check, for the host's own housekeeping: the scheduled deletion an
/// account's owner asked for, once its grace period is over. Roles, claims, logins, the picture and the personal
/// access tokens go with it.
/// <para>
/// Administrators delete through <see cref="IUserAdministrationService.DeleteUserAsync"/>, which checks them first.
/// Never expose this to an endpoint directly.
/// </para>
/// </summary>
public interface IAccountErasure
{
    /// <summary>Deletes the account and records <c>user.deleted</c> with the given actor (null: the current caller).</summary>
    Task<Result> EraseAsync(Guid userId, AuditSubject? actor, CancellationToken cancellationToken);

    /// <summary>The accounts whose owner asked for deletion and whose grace period is over at <paramref name="now"/>.</summary>
    Task<IReadOnlyList<Guid>> DueForDeletionAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
