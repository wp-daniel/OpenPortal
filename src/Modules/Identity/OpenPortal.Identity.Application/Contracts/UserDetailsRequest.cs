using System.ComponentModel.DataAnnotations;
using OpenPortal.Identity.Domain.Users;

namespace OpenPortal.Identity.Application.Contracts;

/// <summary>
/// The personal, work and address details shared by every request that creates or edits an account.
/// Length rules here only produce field-level 400s early; the domain enforces them authoritatively.
/// </summary>
public abstract class UserDetailsRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "validation.firstName.required")]
    [StringLength(ApplicationUser.NameMaxLength, ErrorMessage = "validation.name.max")]
    public string FirstName { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false, ErrorMessage = "validation.lastName.required")]
    [StringLength(ApplicationUser.NameMaxLength, ErrorMessage = "validation.name.max")]
    public string LastName { get; init; } = string.Empty;

    [StringLength(ApplicationUser.PhoneMaxLength, ErrorMessage = "validation.phone.invalid")]
    public string? PhoneNumber { get; init; }

    [StringLength(ApplicationUser.DetailMaxLength, ErrorMessage = "validation.detail.max")]
    public string? JobTitle { get; init; }

    [StringLength(ApplicationUser.DetailMaxLength, ErrorMessage = "validation.detail.max")]
    public string? Company { get; init; }

    [StringLength(ApplicationUser.DetailMaxLength, ErrorMessage = "validation.detail.max")]
    public string? Department { get; init; }

    [StringLength(ApplicationUser.DetailMaxLength, ErrorMessage = "validation.detail.max")]
    public string? AddressLine { get; init; }

    [StringLength(ApplicationUser.DetailMaxLength, ErrorMessage = "validation.detail.max")]
    public string? City { get; init; }

    [StringLength(ApplicationUser.DetailMaxLength, ErrorMessage = "validation.detail.max")]
    public string? PostalCode { get; init; }

    [StringLength(ApplicationUser.DetailMaxLength, ErrorMessage = "validation.detail.max")]
    public string? Country { get; init; }

    public UserDetails ToDetails() => new(
        FirstName, LastName, PhoneNumber, JobTitle, Company, Department, AddressLine, City, PostalCode, Country);
}
