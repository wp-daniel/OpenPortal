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
    public const int LanguageMaxLength = 10;
    public const int NameMaxLength = 60;
    public const int DetailMaxLength = 100;
    public const int PhoneMaxLength = 30;

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

    /// <summary>
    /// The user's preferred UI language as a culture code such as <c>it</c> or <c>pt-BR</c>, or
    /// <see langword="null"/> while no choice has been made. Which codes are actually offered is deployment
    /// configuration, so the domain only guarantees the shape.
    /// </summary>
    public string? Language { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    /// <summary>
    /// When the profile picture (<see cref="UserAvatar"/>) last changed, or <see langword="null"/> when there
    /// is none. Lets the UI decide whether to request an image and version its URL without loading it.
    /// </summary>
    public DateTimeOffset? AvatarUpdatedAtUtc { get; private set; }

    /// <summary>Records that the picture was replaced (a timestamp) or removed (<see langword="null"/>).</summary>
    public void MarkAvatarChanged(DateTimeOffset? changedAtUtc)
    {
        AvatarUpdatedAtUtc = changedAtUtc;
    }

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

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    public string? JobTitle { get; private set; }

    public string? Company { get; private set; }

    public string? Department { get; private set; }

    public string? AddressLine { get; private set; }

    public string? City { get; private set; }

    public string? PostalCode { get; private set; }

    public string? Country { get; private set; }

    /// <summary>
    /// Replaces the personal, work and address details. The phone number is Identity's own
    /// <see cref="IdentityUser{TKey}.PhoneNumber"/>. The display name is derived from the first and last
    /// name so every screen that shows it keeps working.
    /// </summary>
    public SharedKernel.Results.Result UpdateDetails(UserDetails details, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);

        var first = details.FirstName?.Trim() ?? string.Empty;
        var last = details.LastName?.Trim() ?? string.Empty;

        if (first.Length == 0)
        {
            return SharedKernel.Results.Result.Failure(UserErrors.FirstNameRequired);
        }

        if (last.Length == 0)
        {
            return SharedKernel.Results.Result.Failure(UserErrors.LastNameRequired);
        }

        if (first.Length > NameMaxLength || last.Length > NameMaxLength)
        {
            return SharedKernel.Results.Result.Failure(UserErrors.NameTooLong);
        }

        var displayName = $"{first} {last}";
        if (displayName.Length < DisplayNameMinLength)
        {
            return SharedKernel.Results.Result.Failure(UserErrors.DisplayNameTooShort);
        }

        var phone = Clean(details.PhoneNumber);
        if (phone is not null && !IsWellFormedPhoneNumber(phone))
        {
            return SharedKernel.Results.Result.Failure(UserErrors.InvalidPhoneNumber);
        }

        var jobTitle = Clean(details.JobTitle);
        var company = Clean(details.Company);
        var department = Clean(details.Department);
        var addressLine = Clean(details.AddressLine);
        var city = Clean(details.City);
        var postalCode = Clean(details.PostalCode);
        var country = Clean(details.Country);

        if (new[] { jobTitle, company, department, addressLine, city, postalCode, country }
            .Any(value => value is not null && value.Length > DetailMaxLength))
        {
            return SharedKernel.Results.Result.Failure(UserErrors.DetailTooLong);
        }

        FirstName = first;
        LastName = last;
        DisplayName = displayName;
        PhoneNumber = phone;
        JobTitle = jobTitle;
        Company = company;
        Department = department;
        AddressLine = addressLine;
        City = city;
        PostalCode = postalCode;
        Country = country;
        UpdatedAtUtc = now;

        return SharedKernel.Results.Result.Success();
    }

    /// <summary>Digits with an optional leading <c>+</c>; spaces, dots, dashes and brackets are tolerated.</summary>
    public static bool IsWellFormedPhoneNumber(string phone)
    {
        if (phone.Length > PhoneMaxLength)
        {
            return false;
        }

        var digits = 0;
        for (var i = 0; i < phone.Length; i++)
        {
            var c = phone[i];
            if (char.IsAsciiDigit(c))
            {
                digits++;
            }
            else if (!(c == '+' && i == 0) && c is not (' ' or '-' or '.' or '(' or ')'))
            {
                return false;
            }
        }

        return digits is >= 6 and <= 15;
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Stores the preferred UI language. <see langword="null"/> clears the preference.</summary>
    public SharedKernel.Results.Result SetLanguage(string? language, DateTimeOffset now)
    {
        var normalized = string.IsNullOrWhiteSpace(language) ? null : language.Trim();

        if (normalized is not null && !IsWellFormedLanguageCode(normalized))
        {
            return SharedKernel.Results.Result.Failure(UserErrors.UnsupportedLanguage);
        }

        if (!string.Equals(Language, normalized, StringComparison.Ordinal))
        {
            Language = normalized;
            UpdatedAtUtc = now;
        }

        return SharedKernel.Results.Result.Success();
    }

    /// <summary>Whether <paramref name="language"/> looks like a culture code (<c>xx</c> or <c>xx-YY</c>).</summary>
    public static bool IsWellFormedLanguageCode(string language) =>
        language.Length is >= 2 and <= LanguageMaxLength
        && language.All(c => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or '-')
        && !language.StartsWith('-')
        && !language.EndsWith('-');
}
