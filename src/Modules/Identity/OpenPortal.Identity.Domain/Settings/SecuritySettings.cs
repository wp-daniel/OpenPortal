using OpenPortal.Identity.Domain.Users;
using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Identity.Domain.Settings;

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

    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public static SecuritySettings Defaults() => new();

    /// <summary>
    /// Applies new values; the result says whether anything changed. A colon is refused because it separates
    /// the issuer from the account in the authenticator label.
    /// </summary>
    public Result<bool> Update(bool twoFactorEnabled, string? twoFactorIssuer, DateTimeOffset now)
    {
        var issuer = twoFactorIssuer?.Trim();

        if (string.IsNullOrEmpty(issuer) || issuer.Length > IssuerMaxLength || issuer.Contains(':', StringComparison.Ordinal))
        {
            return Result<bool>.Failure(UserErrors.TwoFactorIssuerInvalid);
        }

        if (TwoFactorEnabled == twoFactorEnabled && string.Equals(TwoFactorIssuer, issuer, StringComparison.Ordinal))
        {
            return Result<bool>.Success(false);
        }

        TwoFactorEnabled = twoFactorEnabled;
        TwoFactorIssuer = issuer;
        UpdatedAtUtc = now;

        return Result<bool>.Success(true);
    }
}
