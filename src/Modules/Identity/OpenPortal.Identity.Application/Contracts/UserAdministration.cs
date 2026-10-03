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

    [Range(1, int.MaxValue, ErrorMessage = "validation.page.min")]
    public int Page { get; init; } = 1;

    [Range(1, MaxPageSize, ErrorMessage = "validation.pageSize.range")]
    public int PageSize { get; init; } = 20;

    /// <summary>Free-text filter matched against email and display name.</summary>
    [StringLength(256, ErrorMessage = "validation.search.max")]
    public string? Search { get; init; }
}

/// <summary>A new account created by an administrator.</summary>
public sealed class CreateUserRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "validation.email.required")]
    [EmailAddress(ErrorMessage = "validation.email.invalid")]
    [StringLength(256, ErrorMessage = "validation.email.max")]
    public string Email { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false, ErrorMessage = "validation.password.required")]
    [StringLength(256, ErrorMessage = "validation.password.max")]
    public string Password { get; init; } = string.Empty;

    /// <summary>Optional; defaults to the local part of the email address when omitted.</summary>
    [StringLength(
        ApplicationUser.DisplayNameMaxLength,
        MinimumLength = ApplicationUser.DisplayNameMinLength,
        ErrorMessage = "validation.displayName.length")]
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
    [Required(AllowEmptyStrings = false, ErrorMessage = "validation.displayName.required")]
    [StringLength(
        ApplicationUser.DisplayNameMaxLength,
        MinimumLength = ApplicationUser.DisplayNameMinLength,
        ErrorMessage = "validation.displayName.length")]
    public string DisplayName { get; init; } = string.Empty;

    public IReadOnlyList<string> Roles { get; init; } = [];
}

/// <summary>A new password set by an administrator on another account.</summary>
public sealed class ResetPasswordRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "validation.newPassword.required")]
    [StringLength(256, ErrorMessage = "validation.newPassword.max")]
    public string NewPassword { get; init; } = string.Empty;
}