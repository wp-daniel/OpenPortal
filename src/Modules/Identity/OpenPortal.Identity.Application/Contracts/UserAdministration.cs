using System.ComponentModel.DataAnnotations;
using OpenPortal.Identity.Domain.Users;

namespace OpenPortal.Identity.Application.Contracts;

/// <summary>An account row as rendered by the administration user list.</summary>
public sealed record UserSummaryDto(
    Guid Id,
    string Email,
    string DisplayName,
    bool EmailConfirmed,
    bool IsLockedOut,
    DateTimeOffset? LockoutEndUtc,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<string> Roles);

/// <summary>Filter and paging parameters for the administration user list.</summary>
public sealed class UserListQuery
{
    public const int MaxPageSize = 100;

    [Range(1, int.MaxValue, ErrorMessage = "Page must be 1 or greater.")]
    public int Page { get; init; } = 1;

    [Range(1, MaxPageSize, ErrorMessage = "PageSize must be between 1 and 100.")]
    public int PageSize { get; init; } = 20;

    /// <summary>Free-text filter matched against email and display name.</summary>
    [StringLength(256, ErrorMessage = "Search must not exceed 256 characters.")]
    public string? Search { get; init; }
}

/// <summary>A new account created by an administrator.</summary>
public sealed class CreateUserRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email must be a valid email address.")]
    [StringLength(256, ErrorMessage = "Email must not exceed 256 characters.")]
    public string Email { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false, ErrorMessage = "Password is required.")]
    [StringLength(256, ErrorMessage = "Password must not exceed 256 characters.")]
    public string Password { get; init; } = string.Empty;

    /// <summary>Optional; defaults to the local part of the email address when omitted.</summary>
    [StringLength(
        ApplicationUser.DisplayNameMaxLength,
        MinimumLength = ApplicationUser.DisplayNameMinLength,
        ErrorMessage = "Display name must be between 2 and 120 characters.")]
    public string? DisplayName { get; init; }

    /// <summary>
    /// Requested roles. Filtered against <see cref="Roles.All"/> on the server; unknown names are rejected
    /// rather than silently dropped.
    /// </summary>
    public IReadOnlyList<string> Roles { get; init; } = [];
}

/// <summary>Editable fields and role membership of an existing account.</summary>
public sealed class UpdateUserRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "Display name is required.")]
    [StringLength(
        ApplicationUser.DisplayNameMaxLength,
        MinimumLength = ApplicationUser.DisplayNameMinLength,
        ErrorMessage = "Display name must be between 2 and 120 characters.")]
    public string DisplayName { get; init; } = string.Empty;

    public IReadOnlyList<string> Roles { get; init; } = [];
}

/// <summary>A new password set by an administrator on another account.</summary>
public sealed class ResetPasswordRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "New password is required.")]
    [StringLength(256, ErrorMessage = "New password must not exceed 256 characters.")]
    public string NewPassword { get; init; } = string.Empty;
}