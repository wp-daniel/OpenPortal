using Microsoft.AspNetCore.Identity;
using OpenPortal.SharedKernel.Text;

namespace OpenPortal.Identity.Domain.Users;

/// <summary>How an account came to exist.</summary>
public enum AccountOrigin
{
    /// <summary>Created by an administrator (or the bootstrap step).</summary>
    Created = 0,

    /// <summary>The person registered on their own.</summary>
    Registered = 1,

    /// <summary>The person accepted an invitation.</summary>
    Invited = 2,

    /// <summary>An application's backend created it through the application API.</summary>
    Provisioned = 3,

    /// <summary>Created at the first sign-in with an external account (Google, Microsoft); it has no password.</summary>
    External = 4,
}

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
    public const int DisplayNameMaxLength = 120;
    public const int LanguageMaxLength = 10;
    public const int NameMaxLength = 60;
    public const int DetailMaxLength = 100;
    public const int PhoneMaxLength = 30;

    /// <summary>Required by Entity Framework Core materialisation.</summary>
    private ApplicationUser()
    {
    }

    private ApplicationUser(Guid id, string email, DateTimeOffset createdAtUtc)
    {
        Id = id;
        Email = email;

        // Identity requires a non-empty user name even when sign-in happens by email, and its
        // user-name validator rejects anything but letters and digits. Using the email's local part as a
        // placeholder would produce unstable normalisation, so the address itself is used: this platform
        // has exactly one identifier per account, and a second one that nobody signs in with is a second
        // thing to keep unique, to leak and to explain.
        UserName = email;

        CreatedAtUtc = createdAtUtc;
    }

    /// <summary>
    /// Creates an account with its details, validated first so bad input is a failure rather than an
    /// exception. The display name is derived from the first and last name, as in <see cref="UpdateDetails"/>.
    /// </summary>
    public static SharedKernel.Results.Result<ApplicationUser> Create(
        Guid id,
        string email,
        UserDetails details,
        DateTimeOffset createdAtUtc,
        AccountOrigin origin = AccountOrigin.Created)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        if (!Enum.IsDefined(origin))
        {
            throw new ArgumentOutOfRangeException(nameof(origin), origin, null);
        }

        var user = new ApplicationUser(id, email.Trim(), createdAtUtc) { Origin = origin };

        var named = user.UpdateDetails(details, createdAtUtc);
        if (named.IsFailure)
        {
            return SharedKernel.Results.Result<ApplicationUser>.Failure(named.Error);
        }

        // Filling in the details of a new account is not an update of it.
        user.UpdatedAtUtc = null;

        return SharedKernel.Results.Result<ApplicationUser>.Success(user);
    }

    /// <summary>Human-readable name shown in the UI. Never used as a login credential.</summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>How the account came to exist.</summary>
    public AccountOrigin Origin { get; private set; }

    /// <summary>
    /// True for a self-registered account that waits for an administrator's approval: it cannot sign in until then.
    /// </summary>
    public bool PendingApproval { get; private set; }

    /// <summary>Holds a new self-registered account until an administrator approves it.</summary>
    public void RequireApproval()
    {
        PendingApproval = true;
    }

    /// <summary>Lets an account that waited for approval sign in.</summary>
    public SharedKernel.Results.Result Approve(DateTimeOffset now)
    {
        if (!PendingApproval)
        {
            return SharedKernel.Results.Result.Failure(UserErrors.RegistrationNotPending);
        }

        PendingApproval = false;
        UpdatedAtUtc = now;

        return SharedKernel.Results.Result.Success();
    }

    /// <summary>
    /// The user's preferred UI language as a culture code such as <c>it</c> or <c>pt-BR</c>, or
    /// <see langword="null"/> while no choice has been made. Which codes are actually offered is deployment
    /// configuration, so the domain only guarantees the shape.
    /// </summary>
    public string? Language { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    /// <summary>
    /// When the account last signed in to the portal with its password, or <see langword="null"/> if it never
    /// has. Lets an administrator find dormant accounts.
    /// </summary>
    public DateTimeOffset? LastSignInAtUtc { get; private set; }

    /// <summary>Records a successful sign-in. Not an edit of the account, so <see cref="UpdatedAtUtc"/> stays.</summary>
    public void RecordSignIn(DateTimeOffset signedInAtUtc)
    {
        LastSignInAtUtc = signedInAtUtc;
    }

    /// <summary>
    /// When the profile picture (<see cref="UserAvatar"/>) last changed, or <see langword="null"/> when there
    /// is none. Lets the UI decide whether to request an image and version its URL without loading it.
    /// </summary>
    public DateTimeOffset? AvatarUpdatedAtUtc { get; private set; }

    /// <summary>When the owner asked for the account to be deleted; null when no deletion is scheduled.</summary>
    public DateTimeOffset? DeletionRequestedAtUtc { get; private set; }

    /// <summary>
    /// When the account will be deleted, at the end of the grace period its owner can still cancel in; null when no
    /// deletion is scheduled.
    /// </summary>
    public DateTimeOffset? DeletionScheduledAtUtc { get; private set; }

    /// <summary>Schedules the deletion the owner asked for. Asking again keeps the first date.</summary>
    public void RequestDeletion(DateTimeOffset now, TimeSpan gracePeriod)
    {
        if (DeletionScheduledAtUtc is not null)
        {
            return;
        }

        DeletionRequestedAtUtc = now;
        DeletionScheduledAtUtc = now + gracePeriod;
    }

    /// <summary>Withdraws a scheduled deletion. Returns false when none was scheduled.</summary>
    public bool CancelDeletion()
    {
        if (DeletionScheduledAtUtc is null)
        {
            return false;
        }

        DeletionRequestedAtUtc = null;
        DeletionScheduledAtUtc = null;
        return true;
    }

    /// <summary>
    /// The address the owner asked to sign in with instead of <see cref="IdentityUser{TKey}.Email"/>, until they open
    /// the confirmation link sent to it; null when no change waits.
    /// </summary>
    public string? PendingEmail { get; private set; }

    /// <summary>When <see cref="PendingEmail"/> was asked for.</summary>
    public DateTimeOffset? PendingEmailRequestedAtUtc { get; private set; }

    /// <summary>Remembers the address waiting for its confirmation (asking again replaces it).</summary>
    public void RequestEmailChange(string email, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        PendingEmail = email.Trim();
        PendingEmailRequestedAtUtc = now;
    }

    /// <summary>Forgets the address waiting for its confirmation. Returns false when none was waiting.</summary>
    public bool CancelEmailChange()
    {
        if (PendingEmail is null)
        {
            return false;
        }

        PendingEmail = null;
        PendingEmailRequestedAtUtc = null;
        return true;
    }

    /// <summary>
    /// Makes <paramref name="email"/> the address the account signs in with (it is also the user name) and counts it
    /// as confirmed: either its owner opened a link sent to it, or an administrator vouched for it. The caller
    /// updates the normalised values and the security stamp.
    /// </summary>
    public void ChangeEmail(string email, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        Email = email.Trim();
        UserName = Email;
        EmailConfirmed = true;
        CancelEmailChange();
        UpdatedAtUtc = now;
    }

    /// <summary>Records that the picture was replaced (a timestamp) or removed (<see langword="null"/>).</summary>
    public void MarkAvatarChanged(DateTimeOffset? changedAtUtc)
    {
        AvatarUpdatedAtUtc = changedAtUtc;
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

        var first = TextRules.Normalise(details.FirstName);
        var last = TextRules.Normalise(details.LastName);

        if (first is null)
        {
            return SharedKernel.Results.Result.Failure(UserErrors.FirstNameRequired);
        }

        if (last is null)
        {
            return SharedKernel.Results.Result.Failure(UserErrors.LastNameRequired);
        }

        if (first.Length > NameMaxLength || last.Length > NameMaxLength)
        {
            return SharedKernel.Results.Result.Failure(UserErrors.NameTooLong);
        }

        // Two names within their own limit can still overflow the display name column by the space.
        var displayName = $"{first} {last}";
        if (displayName.Length > DisplayNameMaxLength)
        {
            return SharedKernel.Results.Result.Failure(UserErrors.DisplayNameTooLong);
        }

        var phone = TextRules.Normalise(details.PhoneNumber);
        if (phone is not null && !IsWellFormedPhoneNumber(phone))
        {
            return SharedKernel.Results.Result.Failure(UserErrors.InvalidPhoneNumber);
        }

        var jobTitle = TextRules.Normalise(details.JobTitle);
        var company = TextRules.Normalise(details.Company);
        var department = TextRules.Normalise(details.Department);
        var addressLine = TextRules.Normalise(details.AddressLine);
        var city = TextRules.Normalise(details.City);
        var postalCode = TextRules.Normalise(details.PostalCode);
        var country = TextRules.Normalise(details.Country);

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

    /// <summary>Stores the preferred UI language. <see langword="null"/> clears the preference.</summary>
    public SharedKernel.Results.Result SetLanguage(string? language, DateTimeOffset now)
    {
        var normalized = TextRules.Normalise(language);

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
