using System.ComponentModel.DataAnnotations;
using OpenPortal.Identity.Domain.Users;

namespace OpenPortal.Identity.Application.Contracts;

/// <summary>The signed-in account's own profile.</summary>
public sealed record AccountProfileDto(
    Guid Id,
    string Email,
    string DisplayName,
    bool EmailConfirmed,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    IReadOnlyList<string> Roles,
    string? Language,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    string? JobTitle,
    string? Company,
    string? Department,
    string? AddressLine,
    string? City,
    string? PostalCode,
    string? Country,
    DateTimeOffset? AvatarUpdatedAtUtc,
    string? PendingEmail = null);

/// <summary>Mutable fields of the signed-in account's profile.</summary>
/// <para>
/// Email is intentionally absent. The email address is the login identifier, so changing it goes through
/// <see cref="ChangeEmailRequest"/> and a link sent to the new address; a typo here would lock the account out.
/// </para>
public sealed class UpdateProfileRequest : UserDetailsRequest
{
}

/// <summary>A password change for the signed-in account.</summary>
public sealed class ChangePasswordRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "validation.currentPassword.required")]
    [StringLength(256, ErrorMessage = "validation.currentPassword.max")]
    public string CurrentPassword { get; init; } = string.Empty;

    /// <summary>
    /// Not annotated with a length rule on purpose: the configured complexity policy is published through
    /// the session response and validated authoritatively by the user store.
    /// </summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "validation.newPassword.required")]
    [StringLength(256, ErrorMessage = "validation.newPassword.max")]
    public string NewPassword { get; init; } = string.Empty;
}

/// <summary>Gives a password to an account that has none (one created through an external provider).</summary>
public sealed class SetPasswordRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "validation.newPassword.required")]
    [StringLength(256, ErrorMessage = "validation.newPassword.max")]
    public string NewPassword { get; init; } = string.Empty;
}

/// <summary>Sets (or clears, with <see langword="null"/>) the signed-in account's preferred UI language.</summary>
public sealed class UpdateLanguageRequest
{
    [StringLength(ApplicationUser.LanguageMaxLength, ErrorMessage = "validation.language.length")]
    public string? Language { get; init; }
}

/// <summary>
/// Asks to sign in with another address. Nothing changes until the link sent to that address is opened. The password
/// is required when the account has one; an account without one must have signed in recently instead.
/// </summary>
public sealed class ChangeEmailRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "validation.email.required")]
    [EmailAddress(ErrorMessage = "validation.email.invalid")]
    [StringLength(256, ErrorMessage = "validation.email.max")]
    public string NewEmail { get; init; } = string.Empty;

    [StringLength(256, ErrorMessage = "validation.currentPassword.max")]
    public string? CurrentPassword { get; init; }
}

/// <summary>The link sent to the new address: whose account, and the token proving the address was reached.</summary>
public sealed class ConfirmEmailChangeRequest
{
    [Required(ErrorMessage = "validation.token.required")]
    public Guid UserId { get; init; }

    [Required(AllowEmptyStrings = false, ErrorMessage = "validation.token.required")]
    [StringLength(2048, ErrorMessage = "validation.token.required")]
    public string Token { get; init; } = string.Empty;
}

/// <summary>The address the account signs in with from now on.</summary>
/// <param name="Email">The new address.</param>
/// <param name="SignedIn">Whether the link was opened in that account's own session, which stays signed in.</param>
public sealed record EmailChangeConfirmedDto(string Email, bool SignedIn);
