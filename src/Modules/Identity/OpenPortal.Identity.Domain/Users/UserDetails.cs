namespace OpenPortal.Identity.Domain.Users;

/// <summary>The personal, work and address details of an account, as supplied by an administrator or the user.</summary>
public sealed record UserDetails(
    string? FirstName,
    string? LastName,
    string? PhoneNumber,
    string? JobTitle,
    string? Company,
    string? Department,
    string? AddressLine,
    string? City,
    string? PostalCode,
    string? Country);
