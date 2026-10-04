using System.ComponentModel.DataAnnotations;

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
    IReadOnlyList<string> Roles,
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

/// <summary>Filter and paging parameters for the administration user list.</summary>
public sealed class UserListQuery
{
    public const int MaxPageSize = 100;

    [Range(1, int.MaxValue, ErrorMessage = "validation.page.min")]
    public int Page { get; init; } = 1;

    [Range(1, MaxPageSize, ErrorMessage = "validation.pageSize.range")]
    public int PageSize { get; init; } = 20;

    /// <summary>Free-text filter matched against email, name, phone and company.</summary>
    [StringLength(256, ErrorMessage = "validation.search.max")]
    public string? Search { get; init; }

    /// <summary>Only accounts holding this role.</summary>
    [StringLength(64, ErrorMessage = "validation.search.max")]
    public string? Role { get; init; }

    /// <summary>One of <see cref="UserStatusFilter"/>; anything else is ignored.</summary>
    [StringLength(32, ErrorMessage = "validation.search.max")]
    public string? Status { get; init; }
}

/// <summary>Account states offered as a filter in the user list.</summary>
public static class UserStatusFilter
{
    public const string Active = "active";
    public const string Locked = "locked";
    public const string Unconfirmed = "unconfirmed";
}

/// <summary>A new account created by an administrator.</summary>
public sealed class CreateUserRequest : UserDetailsRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "validation.email.required")]
    [EmailAddress(ErrorMessage = "validation.email.invalid")]
    [StringLength(256, ErrorMessage = "validation.email.max")]
    public string Email { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false, ErrorMessage = "validation.password.required")]
    [StringLength(256, ErrorMessage = "validation.password.max")]
    public string Password { get; init; } = string.Empty;

    /// <summary>
    /// Requested roles. Filtered against <see cref="Roles.All"/> on the server; unknown names are rejected
    /// rather than silently dropped.
    /// </summary>
    public IReadOnlyList<string> Roles { get; init; } = [];
}

/// <summary>Editable fields and role membership of an existing account.</summary>
public sealed class UpdateUserRequest : UserDetailsRequest
{
    public IReadOnlyList<string> Roles { get; init; } = [];
}

/// <summary>A new password set by an administrator on another account.</summary>
public sealed class ResetPasswordRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "validation.newPassword.required")]
    [StringLength(256, ErrorMessage = "validation.newPassword.max")]
    public string NewPassword { get; init; } = string.Empty;
}