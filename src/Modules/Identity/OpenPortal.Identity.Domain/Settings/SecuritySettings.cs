using OpenPortal.Identity.Domain.Users;
using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Identity.Domain.Settings;

/// <summary>Who must protect their account with a second factor (an authenticator app or a passkey).</summary>
public enum TwoFactorRequirement
{
    /// <summary>Nobody: a second factor is offered, not demanded.</summary>
    None = 0,

    /// <summary>Administrators, whose accounts can change everyone else's.</summary>
    Administrators = 1,

    /// <summary>Every account.</summary>
    Everyone = 2,
}

/// <summary>
/// Portal-wide sign-in security settings, edited by administrators from the portal itself. A single row; until
/// an administrator saves it for the first time the defaults below apply.
/// </summary>
public sealed class SecuritySettings
{
    /// <summary>The key of the one row.</summary>
    public const int SingletonId = 1;

    public const int IssuerMaxLength = 64;

    public const string DefaultIssuer = "OpenPortal";

    /// <summary>Required by Entity Framework Core materialisation, and the defaults when no row exists.</summary>
    private SecuritySettings()
    {
    }

    public int Id { get; private set; } = SingletonId;

    /// <summary>
    /// Whether accounts may protect themselves with an authenticator app and are asked for a code at sign-in.
    /// Turning it off keeps every enrolment, so turning it back on restores them as they were.
    /// </summary>
    public bool TwoFactorEnabled { get; private set; }

    /// <summary>The name authenticator apps show next to the account (the <c>issuer</c> of the otpauth URI).</summary>
    public string TwoFactorIssuer { get; private set; } = DefaultIssuer;

    /// <summary>
    /// Whether account holders may create personal access tokens to call the API from scripts. Turning it off makes
    /// every existing token stop working at once, without deleting it.
    /// </summary>
    public bool PersonalAccessTokensEnabled { get; private set; }

    /// <summary>
    /// Whether accounts may sign in with a passkey (WebAuthn: a fingerprint, face or PIN on their device, or a
    /// security key). A passkey is phishing-resistant and counts as a second factor on its own.
    /// </summary>
    public bool PasskeysEnabled { get; private set; } = true;

    /// <summary>
    /// Who must have a second factor. Someone without one signs in, then can do nothing but set one up; while they
    /// have only passkeys, the password alone no longer signs them in.
    /// </summary>
    public TwoFactorRequirement TwoFactorRequirement { get; private set; }

    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public static SecuritySettings Defaults() => new();

    /// <summary>Whether the requirement covers an account with these roles.</summary>
    public bool RequiresSecondFactor(IEnumerable<string> roles) => TwoFactorRequirement switch
    {
        TwoFactorRequirement.Everyone => true,
        TwoFactorRequirement.Administrators => roles.Contains(Roles.Administrator, StringComparer.Ordinal),
        _ => false,
    };

    /// <summary>
    /// Applies new values; the result says whether anything changed. A colon is refused because it separates
    /// the issuer from the account in the authenticator label.
    /// </summary>
    public Result<bool> Update(
        bool twoFactorEnabled,
        string? twoFactorIssuer,
        DateTimeOffset now,
        bool personalAccessTokensEnabled = false,
        bool passkeysEnabled = true,
        TwoFactorRequirement requirement = TwoFactorRequirement.None)
    {
        var issuer = twoFactorIssuer?.Trim();

        if (string.IsNullOrEmpty(issuer) || issuer.Length > IssuerMaxLength || issuer.Contains(':', StringComparison.Ordinal))
        {
            return Result<bool>.Failure(UserErrors.TwoFactorIssuerInvalid);
        }

        if (!Enum.IsDefined(requirement))
        {
            return Result<bool>.Failure(UserErrors.TwoFactorRequirementInvalid);
        }

        // Demanding a second factor nobody can set up would lock everyone out of the portal.
        if (requirement != TwoFactorRequirement.None && !twoFactorEnabled && !passkeysEnabled)
        {
            return Result<bool>.Failure(UserErrors.TwoFactorRequirementUnsatisfiable);
        }

        if (TwoFactorEnabled == twoFactorEnabled
            && string.Equals(TwoFactorIssuer, issuer, StringComparison.Ordinal)
            && PersonalAccessTokensEnabled == personalAccessTokensEnabled
            && PasskeysEnabled == passkeysEnabled
            && TwoFactorRequirement == requirement)
        {
            return Result<bool>.Success(false);
        }

        TwoFactorEnabled = twoFactorEnabled;
        TwoFactorIssuer = issuer;
        PersonalAccessTokensEnabled = personalAccessTokensEnabled;
        PasskeysEnabled = passkeysEnabled;
        TwoFactorRequirement = requirement;
        UpdatedAtUtc = now;

        return Result<bool>.Success(true);
    }
}
