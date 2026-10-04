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
    DateTimeOffset? AvatarUpdatedAtUtc);

/// <summary>Mutable fields of the signed-in account's profile.</summary>
/// <para>
/// Email is intentionally absent. The email address is the login identifier, so changing it requires a
/// confirmation flow; exposing the field here without that flow would let an account lock itself out.
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

/// <summary>Sets (or clears, with <see langword="null"/>) the signed-in account's preferred UI language.</summary>
public sealed class UpdateLanguageRequest
{
    [StringLength(ApplicationUser.LanguageMaxLength, ErrorMessage = "validation.language.length")]
    public string? Language { get; init; }
}
