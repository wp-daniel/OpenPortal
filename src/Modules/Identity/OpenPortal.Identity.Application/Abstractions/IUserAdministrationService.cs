using OpenPortal.Identity.Application.Contracts;
using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Identity.Application.Abstractions;

/// <summary>
/// Administrative account management.
/// <para>
/// Authorisation is enforced twice: the endpoint requires the administrator policy, and the implementation
/// re-checks the caller's role before mutating anything. The service is therefore safe to call from any
/// future host or module without inheriting a new endpoint's policy by accident.
/// </para>
/// </summary>
public interface IUserAdministrationService
{
    Task<Result<PagedResult<UserSummaryDto>>> ListUsersAsync(
        UserListQuery query,
        CancellationToken cancellationToken);

    Task<Result<UserSummaryDto>> GetUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Creates an account. The password is validated against the configured complexity policy by the
    /// underlying user store, and role names are filtered against the server-side allowlist.
    /// </summary>
    Task<Result<UserSummaryDto>> CreateUserAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates an ordinary account (the <c>User</c> role only), for a caller allowed
    /// <see cref="UserAdministrationOperation.ProvisionUsers"/> such as an application's backend. The password is
    /// validated against the configured policy, like <see cref="CreateUserAsync"/>.
    /// </summary>
    Task<Result<UserSummaryDto>> ProvisionUserAsync(
        ProvisionUserRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Updates the mutable fields and role membership of an account. An administrator cannot change their
    /// own roles, which prevents both self-lockout and confused-deputy role grants.
    /// </summary>
    Task<Result<UserSummaryDto>> UpdateUserAsync(
        Guid userId,
        UpdateUserRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Sets a new password for an account and rotates its security stamp, terminating that account's
    /// existing sessions.
    /// </summary>
    Task<Result> ResetPasswordAsync(
        Guid userId,
        ResetPasswordRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Turns two-factor authentication off for an account whose owner lost their authenticator, discarding the
    /// key and recovery codes. Rotates the security stamp, so the account's sessions end.
    /// </summary>
    Task<Result> ResetTwoFactorAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Lets a self-registered account that waited for approval sign in, and tells its owner. Rejecting one is
    /// deleting it.
    /// </summary>
    Task<Result<UserSummaryDto>> ApproveRegistrationAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes an account. Nobody can delete themselves, and only an administrator can delete an
    /// administrator. What other modules hold about the user (groups, grants) is the host's to remove.
    /// </summary>
    Task<Result> DeleteUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Every account matching the filter, sorted by email, up to <paramref name="maxRows"/>, for a download. Needs
    /// <see cref="UserAdministrationOperation.ExchangeUsers"/>; recorded in the audit log.
    /// </summary>
    Task<Result<IReadOnlyList<UserSummaryDto>>> ExportUsersAsync(UserListFilter filter, int maxRows, CancellationToken cancellationToken);

    /// <summary>
    /// Ends every portal session of the account (its security stamp rotates) and its personal access tokens. Not
    /// yourself; an administrator only by an administrator. The host also revokes its application tokens.
    /// </summary>
    Task<Result> RevokeSessionsAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Records the outcome of a user file import (the rows themselves are audited one by one).</summary>
    Task RecordImportAsync(IReadOnlyDictionary<string, string?> summary, CancellationToken cancellationToken);
}
