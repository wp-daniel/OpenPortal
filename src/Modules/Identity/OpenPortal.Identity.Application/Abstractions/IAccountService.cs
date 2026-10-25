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

    /// <summary>
    /// Gives the signed-in account a password when it has none (it was created through an external provider).
    /// Refused with <c>identity.password_already_set</c> otherwise: changing one needs the current password.
    /// </summary>
    Task<Result> SetPasswordAsync(SetPasswordRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Sends a confirmation link to the address the caller wants to sign in with; the account keeps its address until
    /// the link is opened (<see cref="ConfirmEmailChangeAsync"/>). The answer is the same whether or not another account
    /// uses that address, so it cannot be used to find accounts: that account's owner is told instead.
    /// </summary>
    Task<Result<AccountProfileDto>> RequestEmailChangeAsync(ChangeEmailRequest request, CancellationToken cancellationToken);

    /// <summary>Forgets the address waiting for confirmation; its link stops working.</summary>
    Task<Result<AccountProfileDto>> CancelEmailChangeAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Applies the change the link was sent for. Anonymous: the link may be opened in another browser. The old address
    /// is told, the security stamp rotates and the account's other sessions end.
    /// </summary>
    Task<Result<EmailChangeConfirmedDto>> ConfirmEmailChangeAsync(ConfirmEmailChangeRequest request, CancellationToken cancellationToken);
}