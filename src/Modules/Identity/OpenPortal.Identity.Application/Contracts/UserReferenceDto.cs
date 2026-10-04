namespace OpenPortal.Identity.Application.Contracts;

/// <summary>The minimum needed to name an account and decide whether it may sign in.</summary>
/// <param name="IsActive">False while the account is locked out.</param>
public sealed record UserReferenceDto(Guid Id, string Email, string DisplayName, bool IsActive);
