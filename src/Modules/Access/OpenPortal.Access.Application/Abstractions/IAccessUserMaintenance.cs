namespace OpenPortal.Access.Application.Abstractions;

/// <summary>
/// What the Access module holds about one account, for the host's own housekeeping: erasing a deleted account
/// (by an administrator, by its owner's request once the grace period ends, or in bulk) and ending every session
/// an account has in the applications.
/// <para>
/// No caller check: the host calls it only after it has authorized the operation it is part of (or from a
/// background job that has no caller). Never expose it to an endpoint directly.
/// </para>
/// </summary>
public interface IAccessUserMaintenance
{
    /// <summary>
    /// Drops the account's direct grants, group and organization memberships and access requests, and revokes
    /// every token it holds. Forgetting an account that holds nothing succeeds.
    /// </summary>
    Task ForgetUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Revokes every authorization and token the account holds in every application, so each application loses
    /// the session at its next refresh at the latest. Grants and memberships stay.
    /// </summary>
    Task RevokeTokensAsync(Guid userId, CancellationToken cancellationToken);
}
