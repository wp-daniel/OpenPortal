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
/// <param name="PersonalAccessTokensEnabled">Whether account holders may create personal access tokens for the API.</param>
/// <param name="PasskeysEnabled">Whether accounts may sign in with passkeys.</param>
/// <param name="TwoFactorRequirement"><c>none</c>, <c>administrators</c> or <c>everyone</c>: who must have a second factor.</param>
public sealed record SecuritySettingsDto(
    bool TwoFactorEnabled,
    string TwoFactorIssuer,
    bool PersonalAccessTokensEnabled,
    DateTimeOffset? UpdatedAtUtc,
    bool PasskeysEnabled,
    string TwoFactorRequirement,
    int IdleTimeoutMinutes = 0);

/// <summary>New values for the portal's sign-in security settings.</summary>
public sealed class UpdateSecuritySettingsRequest
{
    public bool TwoFactorEnabled { get; init; }

    [Required(AllowEmptyStrings = false, ErrorMessage = "validation.twoFactorIssuer.required")]
    [StringLength(SecuritySettings.IssuerMaxLength, ErrorMessage = "validation.twoFactorIssuer.max")]
    public string TwoFactorIssuer { get; init; } = SecuritySettings.DefaultIssuer;

    public bool PersonalAccessTokensEnabled { get; init; }

    public bool PasskeysEnabled { get; init; } = true;

    /// <summary><c>none</c>, <c>administrators</c> or <c>everyone</c>.</summary>
    public string TwoFactorRequirement { get; init; } = "none";

    /// <summary>Minutes without activity after which a session ends; 0 keeps sessions until they expire.</summary>
    public int IdleTimeoutMinutes { get; init; }
}

/// <summary>The names of <see cref="Domain.Settings.TwoFactorRequirement"/> in the API.</summary>
public static class TwoFactorRequirements
{
    public const string None = "none";
    public const string Administrators = "administrators";
    public const string Everyone = "everyone";

    public static string ToContract(Domain.Settings.TwoFactorRequirement requirement) => requirement switch
    {
        Domain.Settings.TwoFactorRequirement.Administrators => Administrators,
        Domain.Settings.TwoFactorRequirement.Everyone => Everyone,
        _ => None,
    };

    /// <summary>The requirement a name stands for; an unknown name maps to an undefined value the domain refuses.</summary>
    public static Domain.Settings.TwoFactorRequirement FromContract(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        null or "" or None => Domain.Settings.TwoFactorRequirement.None,
        Administrators => Domain.Settings.TwoFactorRequirement.Administrators,
        Everyone => Domain.Settings.TwoFactorRequirement.Everyone,
        _ => (Domain.Settings.TwoFactorRequirement)(-1),
    };
}

/// <summary>One passkey of an account.</summary>
/// <param name="Id">The credential id, base64url-encoded (as WebAuthn writes it).</param>
/// <param name="BackedUp">Whether the provider syncs it across devices (a password manager or a phone's keychain).</param>
public sealed record PasskeyDto(string Id, string Name, DateTimeOffset CreatedAtUtc, bool BackedUp);

public sealed class RegisterPasskeyRequest
{
    /// <summary>The JSON of the <c>PublicKeyCredential</c> that <c>navigator.credentials.create()</c> returned.</summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "validation.passkey.required")]
    [StringLength(65536, ErrorMessage = "validation.passkey.required")]
    public string Credential { get; init; } = string.Empty;

    /// <summary>A name to tell it apart ("Laptop", "YubiKey"); empty picks one.</summary>
    [StringLength(64, ErrorMessage = "validation.passkeyName.max")]
    public string? Name { get; init; }
}

public sealed class RenamePasskeyRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "validation.passkeyName.required")]
    [StringLength(64, ErrorMessage = "validation.passkeyName.max")]
    public string Name { get; init; } = string.Empty;
}

/// <summary>Signs in with a passkey: the JSON of the credential <c>navigator.credentials.get()</c> returned.</summary>
public sealed class PasskeySignInRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "validation.passkey.required")]
    [StringLength(65536, ErrorMessage = "validation.passkey.required")]
    public string Credential { get; init; } = string.Empty;

    public bool RememberMe { get; init; }
}
