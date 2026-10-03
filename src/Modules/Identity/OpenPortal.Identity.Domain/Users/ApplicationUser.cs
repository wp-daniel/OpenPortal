using Microsoft.AspNetCore.Identity;

namespace OpenPortal.Identity.Domain.Users;

/// <summary>
/// The platform's user account.
/// <para>
/// Extends <see cref="IdentityUser{TKey}"/> rather than replacing it, so the account inherits ASP.NET Core
/// Identity's PBKDF2 password hashing, lockout state, security stamp and token-provider plumbing. Those
/// mechanisms are the reason this project does not implement its own credential store.
/// </para>
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public const int DisplayNameMinLength = 2;
    public const int DisplayNameMaxLength = 120;

    /// <summary>Required by Entity Framework Core materialisation.</summary>
    private ApplicationUser()
    {
    }

    public ApplicationUser(Guid id, string email, string displayName, DateTimeOffset createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        var trimmedEmail = email.Trim();

        Id = id;
        Email = trimmedEmail;

        // Identity requires a non-empty user name even when sign-in happens by email, and its
        // user-name validator rejects anything but letters and digits. Using the email's local part as a
        // placeholder would produce unstable normalisation, so the address itself is used: this platform
        // has exactly one identifier per account, and a second one that nobody signs in with is a second
        // thing to keep unique, to leak and to explain.
        UserName = trimmedEmail;

        DisplayName = displayName.Trim();
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary>Human-readable name shown in the UI. Never used as a login credential.</summary>
    public string DisplayName { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    /// <summary>
    /// Renames the account, rejecting names the UI could not render meaningfully.
    /// </summary>
    public SharedKernel.Results.Result UpdateDisplayName(string displayName, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(displayName);

        var normalized = displayName.Trim();

        if (normalized.Length < DisplayNameMinLength)
        {
            return SharedKernel.Results.Result.Failure(UserErrors.DisplayNameTooShort);
        }

        if (normalized.Length > DisplayNameMaxLength)
        {
            return SharedKernel.Results.Result.Failure(UserErrors.DisplayNameTooLong);
        }

        if (!string.Equals(DisplayName, normalized, StringComparison.Ordinal))
        {
            DisplayName = normalized;
            UpdatedAtUtc = now;
        }

        return SharedKernel.Results.Result.Success();
    }
}