using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Access.Application.Contracts;
using OpenPortal.Identity.Application.Abstractions;

namespace OpenPortal.Web.Authentication;

/// <summary>
/// Gives the Access module names and emails for the user ids it stores, from the Identity module. The two
/// modules never reference each other; this adapter is the only place that knows both.
/// </summary>
internal sealed class IdentityUserDirectory : IUserDirectory
{
    private readonly IUserLookupService _users;

    public IdentityUserDirectory(IUserLookupService users)
    {
        _users = users;
    }

    public async Task<IReadOnlyDictionary<Guid, DirectoryUser>> FindAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken)
    {
        var users = await _users.FindManyAsync(userIds, cancellationToken).ConfigureAwait(false);

        return users.ToDictionary(user => user.Id, user => new DirectoryUser(user.Id, user.Email, user.DisplayName));
    }
}
