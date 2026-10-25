using System.ComponentModel.DataAnnotations;
using OpenPortal.Identity.Domain.Settings;

namespace OpenPortal.Identity.Application.Contracts;

/// <summary>The contract names of <see cref="ExternalSignUpMode"/>.</summary>
public static class ExternalSignUpModes
{
    public const string Disabled = "disabled";
    public const string Open = "open";
    public const string Approval = "approval";

    public static string ToContract(ExternalSignUpMode mode) => mode switch
    {
        ExternalSignUpMode.Open => Open,
        ExternalSignUpMode.Approval => Approval,
        _ => Disabled,
    };

    /// <summary>The mode a name stands for; an unknown name maps to an undefined value the domain refuses.</summary>
    public static ExternalSignUpMode FromContract(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        null or "" or Disabled => ExternalSignUpMode.Disabled,
        Open => ExternalSignUpMode.Open,
        Approval => ExternalSignUpMode.Approval,
        _ => (ExternalSignUpMode)(-1),
    };
}

/// <summary>One external sign-in provider as the settings screen shows it. The client secret is never returned.</summary>
/// <param name="Key"><c>google</c> or <c>microsoft</c>; also the last segment of <see cref="CallbackPath"/>.</param>
/// <param name="CallbackPath">
/// The path to register as redirect URI at the provider, appended to the portal's public address
/// (<c>https://portal.example.com/signin-google</c>).
/// </param>
/// <param name="Tenant">Microsoft only: <c>organizations</c>, <c>common</c>, <c>consumers</c> or one directory.</param>
/// <param name="SignUp"><c>disabled</c>, <c>open</c> or <c>approval</c>: what happens to an external account the portal does not know.</param>
public sealed record ExternalProviderSettingsDto(
    string Key,
    string DisplayName,
    bool Enabled,
    string? ClientId,
    bool HasClientSecret,
    string? Tenant,
    string SignUp,
    IReadOnlyList<string> AllowedDomains,
    string CallbackPath,
    DateTimeOffset? UpdatedAtUtc);

/// <summary>Changes one provider.</summary>
public sealed class UpdateExternalProviderRequest
{
    public bool Enabled { get; init; }

    [StringLength(ExternalLoginProvider.ClientIdMaxLength, ErrorMessage = "validation.externalClientId.max")]
    public string? ClientId { get; init; }

    /// <summary>A new client secret; omitted (or empty) keeps the stored one.</summary>
    [StringLength(1000, ErrorMessage = "validation.externalClientSecret.max")]
    public string? ClientSecret { get; init; }

    [StringLength(ExternalLoginProvider.TenantMaxLength, ErrorMessage = "validation.externalTenant.max")]
    public string? Tenant { get; init; }

    public string? SignUp { get; init; }

    public IReadOnlyList<string>? AllowedDomains { get; init; }
}

/// <summary>A provider the sign-in page offers.</summary>
public sealed record ExternalProviderInfoDto(string Key, string DisplayName);

/// <summary>An external account connected to a portal account.</summary>
/// <param name="Provider"><c>google</c> or <c>microsoft</c>.</param>
/// <param name="AccountName">The external account's address as it was when connected, to tell several apart.</param>
public sealed record ExternalLoginDto(string Provider, string DisplayName, string? AccountName);

/// <summary>The signed-in account's external sign-ins.</summary>
/// <param name="HasPassword">Whether the account has a password (one created through a provider has none until it sets one).</param>
/// <param name="Available">Providers that are on and not connected yet, which the account may connect.</param>
public sealed record AccountExternalLoginsDto(
    bool HasPassword,
    IReadOnlyList<ExternalLoginDto> Logins,
    IReadOnlyList<ExternalProviderInfoDto> Available);

/// <summary>What became of an external sign-in.</summary>
public static class ExternalSignInOutcomes
{
    /// <summary>A session was established.</summary>
    public const string SignedIn = "signed_in";

    /// <summary>The account has two-factor authentication: the code is asked for next, as after a password.</summary>
    public const string TwoFactorRequired = "two_factor_required";

    /// <summary>A new account was created and waits for an administrator's approval.</summary>
    public const string ApprovalPending = "approval_pending";
}

/// <summary>Items of the provider challenge that the sign-in reads back after the round trip.</summary>
public static class ExternalLoginProperties
{
    /// <summary><c>true</c> to keep the session after the browser closes ("keep me signed in").</summary>
    public const string RememberMe = "openportal.remember";
}

/// <summary>
/// The claims the host's provider handlers put on the external identity before the Identity module reads it, so the
/// module needs no knowledge of each provider's token format.
/// </summary>
public static class ExternalLoginClaimTypes
{
    /// <summary>The provider's stable id for the person (Google's <c>sub</c>, Microsoft's <c>tid:oid</c>).</summary>
    public const string Subject = System.Security.Claims.ClaimTypes.NameIdentifier;

    public const string Email = "openportal:email";

    /// <summary><c>true</c> when the provider vouches that the person controls <see cref="Email"/>.</summary>
    public const string EmailVerified = "openportal:email_verified";

    public const string GivenName = "openportal:given_name";

    public const string FamilyName = "openportal:family_name";

    public const string Name = "openportal:name";
}
