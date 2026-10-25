namespace OpenPortal.Identity.Application.Contracts;

/// <summary>The minimum needed to name an account and decide whether it may sign in.</summary>
/// <param name="IsActive">False while the account is locked out.</param>
/// <param name="DeletionScheduled">Its owner asked for it to be deleted: it signs in to nothing but the portal.</param>
/// <param name="EmailConfirmed">Whether the account proved it controls its address.</param>
public sealed record UserReferenceDto(
    Guid Id,
    string Email,
    string DisplayName,
    bool IsActive,
    bool DeletionScheduled = false,
    bool EmailConfirmed = false,
    string? Language = null);

/// <summary>What the standard OpenID Connect claims say about an account (OpenID Connect Core, section 5.1).</summary>
/// <param name="UpdatedAtUtc">The last change of the account's details (its creation when never changed).</param>
/// <param name="Country">An ISO 3166 alpha-2 code, or an older free-text value.</param>
public sealed record UserClaimsProfileDto(
    string FirstName,
    string LastName,
    string? Language,
    string? PhoneNumber,
    bool PhoneNumberConfirmed,
    string? AddressLine,
    string? City,
    string? PostalCode,
    string? Country,
    DateTimeOffset UpdatedAtUtc);
