namespace OpenPortal.Identity.Application.Contracts;

/// <summary>The minimum needed to name an account and decide whether it may sign in.</summary>
/// <param name="IsActive">False while the account is locked out.</param>
/// <param name="DeletionScheduled">Its owner asked for it to be deleted: it signs in to nothing but the portal.</param>
public sealed record UserReferenceDto(Guid Id, string Email, string DisplayName, bool IsActive, bool DeletionScheduled = false);
