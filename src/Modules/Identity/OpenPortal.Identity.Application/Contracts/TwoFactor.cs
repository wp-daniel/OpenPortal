using System.ComponentModel.DataAnnotations;
using OpenPortal.Identity.Domain.Settings;

namespace OpenPortal.Identity.Application.Contracts;

/// <summary>The signed-in account's two-factor state.</summary>
/// <param name="Available">Whether the portal offers two-factor authentication at all (a portal setting).</param>
/// <param name="Enabled">Whether this account has an authenticator app enrolled.</param>
/// <param name="RecoveryCodesLeft">Unused recovery codes; each works once.</param>
public sealed record TwoFactorStatusDto(bool Available, bool Enabled, int RecoveryCodesLeft);

/// <summary>What the authenticator app needs: the key to type, and the same key as an otpauth URI for a QR code.</summary>
public sealed record TwoFactorSetupDto(string SharedKey, string AuthenticatorUri);

/// <summary>Freshly generated recovery codes. They are shown once and only their count can be read later.</summary>
public sealed record RecoveryCodesDto(IReadOnlyList<string> Codes);

/// <summary>Turns two-factor authentication on, proving the app was set up by sending one of its codes.</summary>
public sealed class EnableTwoFactorRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "validation.twoFactorCode.required")]
    [StringLength(16, ErrorMessage = "validation.twoFactorCode.max")]
    public string Code { get; init; } = string.Empty;
}

/// <summary>Turning two-factor authentication off, or replacing the recovery codes, asks for the password again.</summary>
public sealed class TwoFactorPasswordRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "validation.currentPassword.required")]
    [StringLength(256, ErrorMessage = "validation.currentPassword.max")]
    public string Password { get; init; } = string.Empty;
}

/// <summary>The second sign-in step, after the password was accepted.</summary>
public sealed class TwoFactorLoginRequest
{
    /// <summary>A code from the authenticator app, or a recovery code when <see cref="UseRecoveryCode"/> is set.</summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "validation.twoFactorCode.required")]
    [StringLength(32, ErrorMessage = "validation.twoFactorCode.max")]
    public string Code { get; init; } = string.Empty;

    public bool UseRecoveryCode { get; init; }

    /// <summary>The "keep me signed in" choice of the first step, which the server does not keep in between.</summary>
    public bool RememberMe { get; init; }

    /// <summary>Skip the code on this browser next time (until the account's security stamp changes).</summary>
    public bool RememberBrowser { get; init; }
}

/// <summary>The portal's sign-in security settings.</summary>
public sealed record SecuritySettingsDto(bool TwoFactorEnabled, string TwoFactorIssuer, DateTimeOffset? UpdatedAtUtc);

/// <summary>New values for the portal's sign-in security settings.</summary>
public sealed class UpdateSecuritySettingsRequest
{
    public bool TwoFactorEnabled { get; init; }

    [Required(AllowEmptyStrings = false, ErrorMessage = "validation.twoFactorIssuer.required")]
    [StringLength(SecuritySettings.IssuerMaxLength, ErrorMessage = "validation.twoFactorIssuer.max")]
    public string TwoFactorIssuer { get; init; } = SecuritySettings.DefaultIssuer;
}
