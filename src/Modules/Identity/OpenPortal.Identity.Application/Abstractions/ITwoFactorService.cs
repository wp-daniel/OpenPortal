using OpenPortal.Identity.Application.Contracts;
using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Identity.Application.Abstractions;

/// <summary>
/// The signed-in account's own authenticator-app enrolment (TOTP, RFC 6238). Nothing to configure outside the
/// portal: the key lives in the user store and any authenticator app reads it from a QR code. Scoped to
/// <see cref="ICurrentUser"/>, like <see cref="IAccountService"/>.
/// </summary>
public interface ITwoFactorService
{
    Task<Result<TwoFactorStatusDto>> GetStatusAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Returns the key to add to the authenticator app, creating one if the account has none. Refused while the
    /// portal setting is off or the account is already enrolled.
    /// </summary>
    Task<Result<TwoFactorSetupDto>> BeginSetupAsync(CancellationToken cancellationToken);

    /// <summary>Turns two-factor on once a code from the app checks out, and returns the first recovery codes.</summary>
    Task<Result<RecoveryCodesDto>> EnableAsync(EnableTwoFactorRequest request, CancellationToken cancellationToken);

    /// <summary>Turns two-factor off after checking the password; the key and recovery codes are discarded.</summary>
    Task<Result> DisableAsync(TwoFactorPasswordRequest request, CancellationToken cancellationToken);

    /// <summary>Replaces every recovery code after checking the password.</summary>
    Task<Result<RecoveryCodesDto>> RegenerateRecoveryCodesAsync(
        TwoFactorPasswordRequest request,
        CancellationToken cancellationToken);
}

/// <summary>Reads and changes the portal's sign-in security settings. Administrators only, never delegated.</summary>
public interface ISecuritySettingsService
{
    Task<Result<SecuritySettingsDto>> GetAsync(CancellationToken cancellationToken);

    Task<Result<SecuritySettingsDto>> UpdateAsync(
        UpdateSecuritySettingsRequest request,
        CancellationToken cancellationToken);
}
