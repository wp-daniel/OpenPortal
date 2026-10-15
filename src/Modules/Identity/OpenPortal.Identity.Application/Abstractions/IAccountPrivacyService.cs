using OpenPortal.Identity.Application.Contracts;
using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Identity.Application.Abstractions;

/// <summary>
/// The account holder's own privacy rights: proving it is them before their data is handed over, and asking for
/// (or withdrawing) the deletion of their account. Always the signed-in caller's own account.
/// </summary>
public interface IAccountPrivacyService
{
    /// <summary>Checks the caller's password (400 <c>identity.password_incorrect</c> when wrong); a failure is audited.</summary>
    Task<Result> ConfirmPasswordAsync(string password, string action, CancellationToken cancellationToken);

    /// <summary>
    /// Schedules the deletion of the caller's account after the grace period and ends its portal sessions (the
    /// security stamp rotates). The host also ends its sessions in the applications and signs the caller out.
    /// </summary>
    Task<Result<AccountDeletionDto>> RequestDeletionAsync(PasswordConfirmationRequest request, CancellationToken cancellationToken);

    /// <summary>Withdraws the caller's scheduled deletion.</summary>
    Task<Result> CancelDeletionAsync(CancellationToken cancellationToken);
}

/// <summary>What account holders may do with their own data. Administrators only, never delegated.</summary>
public interface IPrivacySettingsService
{
    Task<Result<PrivacySettingsDto>> GetAsync(CancellationToken cancellationToken);

    Task<Result<PrivacySettingsDto>> UpdateAsync(UpdatePrivacySettingsRequest request, CancellationToken cancellationToken);
}
