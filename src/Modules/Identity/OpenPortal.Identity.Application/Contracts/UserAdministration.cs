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
    bool IsAdministrator,
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
    DateTimeOffset? LastSignInAtUtc,
    bool TwoFactorEnabled,
    bool PendingApproval,
    string Origin,
    DateTimeOffset? DeletionScheduledAtUtc);

/// <summary>Values of <see cref="UserSummaryDto.Origin"/>.</summary>
public static class AccountOrigins
{
    public const string Created = "created";
    public const string Registered = "registered";
    public const string Invited = "invited";
    public const string Provisioned = "provisioned";
}

/// <summary>Filter and paging parameters for the administration user list.</summary>
public sealed class UserListQuery : UserListFilter
{
    public const int MaxPageSize = 100;

    [Range(1, int.MaxValue, ErrorMessage = "validation.page.min")]
    public int Page { get; init; } = 1;

    [Range(1, MaxPageSize, ErrorMessage = "validation.pageSize.range")]
    public int PageSize { get; init; } = 20;
}

/// <summary>Filters shared by the user list and its export.</summary>
public class UserListFilter
{
    /// <summary>Free-text filter matched against email, name, phone and company.</summary>
    [StringLength(256, ErrorMessage = "validation.search.max")]
    public string? Search { get; init; }

    /// <summary>Only administrators (<c>true</c>) or only the other accounts (<c>false</c>); all when absent.</summary>
    public bool? Administrator { get; init; }

    /// <summary>One of <see cref="UserStatusFilter"/>; anything else is ignored.</summary>
    [StringLength(32, ErrorMessage = "validation.search.max")]
    public string? Status { get; init; }

    /// <summary>Only these accounts (a selection in the list); all matching accounts when absent.</summary>
    [MaxLength(UserBulkLimits.MaxUsers, ErrorMessage = "validation.selection.max")]
    public Guid[]? Ids { get; init; }
}

/// <summary>Limits of the operations on many accounts at once.</summary>
public static class UserBulkLimits
{
    /// <summary>The most accounts one bulk action (or one selection) names.</summary>
    public const int MaxUsers = 100;

    /// <summary>The most rows one export carries.</summary>
    public const int MaxExportRows = 10_000;
}

/// <summary>Account states offered as a filter in the user list.</summary>
public static class UserStatusFilter
{
    public const string Active = "active";
    public const string Locked = "locked";
    public const string Unconfirmed = "unconfirmed";

    /// <summary>Self-registered, waiting for an administrator.</summary>
    public const string Pending = "pending";

    /// <summary>Never signed in, or not for <see cref="InactiveAfterDays"/> days.</summary>
    public const string Inactive = "inactive";

    /// <summary>Their owner asked for them to be deleted; they go when the grace period ends.</summary>
    public const string Deletion = "deletion";

    public const int InactiveAfterDays = 90;
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

    /// <summary>Whether the account is an administrator. Only an administrator may set it.</summary>
    public bool IsAdministrator { get; init; }
}

/// <summary>A new ordinary account created on someone else's behalf, never an administrator.</summary>
public sealed class ProvisionUserRequest : UserDetailsRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "validation.email.required")]
    [EmailAddress(ErrorMessage = "validation.email.invalid")]
    [StringLength(256, ErrorMessage = "validation.email.max")]
    public string Email { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false, ErrorMessage = "validation.password.required")]
    [StringLength(256, ErrorMessage = "validation.password.max")]
    public string Password { get; init; } = string.Empty;
}

/// <summary>Editable fields of an existing account, and whether it is an administrator.</summary>
public sealed class UpdateUserRequest : UserDetailsRequest
{
    /// <summary>Whether the account is an administrator. Only an administrator may change it.</summary>
    public bool IsAdministrator { get; init; }
}

/// <summary>A new password set by an administrator on another account.</summary>
public sealed class ResetPasswordRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "validation.newPassword.required")]
    [StringLength(256, ErrorMessage = "validation.newPassword.max")]
    public string NewPassword { get; init; } = string.Empty;
}