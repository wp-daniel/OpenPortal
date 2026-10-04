using OpenPortal.Identity.Application.Contracts;

namespace OpenPortal.Identity.Application.Abstractions;

/// <summary>
/// Read-only lookup of accounts by id, for the host's own use: resolving names for the access screens and
/// identifying the user behind an OpenID Connect sign-in.
/// <para>
/// Unlike <see cref="IUserAdministrationService"/> it performs no caller check, because the sign-in
/// endpoints call it for ordinary users. It therefore exposes only what any signed-in user's own token may
/// carry (id, email, display name) and must not be put behind an endpoint as it is.
/// </para>
/// </summary>
public interface IUserLookupService
{
    Task<UserReferenceDto?> FindAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Returns the accounts that exist among <paramref name="userIds"/>; unknown ids are left out.</summary>
    Task<IReadOnlyList<UserReferenceDto>> FindManyAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken);
}
