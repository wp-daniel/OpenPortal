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
    /// Deletes an account. Nobody can delete themselves, and only an administrator can delete an
    /// administrator. What other modules hold about the user (groups, grants) is the host's to remove.
    /// </summary>
    Task<Result> DeleteUserAsync(Guid userId, CancellationToken cancellationToken);
}