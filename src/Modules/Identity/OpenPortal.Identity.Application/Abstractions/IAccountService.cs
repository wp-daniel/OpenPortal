using OpenPortal.Identity.Application.Contracts;
using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Identity.Application.Abstractions;

/// <summary>
/// Self-service operations a signed-in account may perform on itself. Scoped to
/// <see cref="ICurrentUser"/>: a caller can never address another account through this contract.
/// </summary>
public interface IAccountService
{
    Task<Result<AccountProfileDto>> GetProfileAsync(CancellationToken cancellationToken);

    /// <summary>Updates the mutable profile fields of the signed-in account.</summary>
    Task<Result<AccountProfileDto>> UpdateProfileAsync(
        UpdateProfileRequest request,
        CancellationToken cancellationToken);

    /// <summary>Stores the caller's preferred UI language after checking it is one the deployment offers.</summary>
    Task<Result<AccountProfileDto>> UpdateLanguageAsync(
        UpdateLanguageRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the caller's password after verifying the current one. Changing the password rotates the
    /// security stamp, which invalidates every other session belonging to the account.
    /// </summary>
    Task<Result> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken);
}