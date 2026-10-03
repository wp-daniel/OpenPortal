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
    IReadOnlyList<string> Roles);

/// <summary>Mutable fields of the signed-in account's profile.</summary>
/// <para>
/// Email is intentionally absent. The email address is the login identifier, so changing it requires a
/// confirmation flow; exposing the field here without that flow would let an account lock itself out.
/// </para>
public sealed class UpdateProfileRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "Display name is required.")]
    [StringLength(
        ApplicationUser.DisplayNameMaxLength,
        MinimumLength = ApplicationUser.DisplayNameMinLength,
        ErrorMessage = "Display name must be between 2 and 120 characters.")]
    public string DisplayName { get; init; } = string.Empty;
}

/// <summary>A password change for the signed-in account.</summary>
public sealed class ChangePasswordRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "Current password is required.")]
    [StringLength(256, ErrorMessage = "Current password must not exceed 256 characters.")]
    public string CurrentPassword { get; init; } = string.Empty;

    /// <summary>
    /// Not annotated with a length rule on purpose: the configured complexity policy is published through
    /// the session response and validated authoritatively by the user store.
    /// </summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "New password is required.")]
    [StringLength(256, ErrorMessage = "New password must not exceed 256 characters.")]
    public string NewPassword { get; init; } = string.Empty;
}