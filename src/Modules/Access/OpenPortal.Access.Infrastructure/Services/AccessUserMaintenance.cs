using Microsoft.EntityFrameworkCore;
using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Access.Infrastructure.Persistence;

namespace OpenPortal.Access.Infrastructure.Services;

/// <summary>Erases an account's access data and ends its application sessions. See <see cref="IAccessUserMaintenance"/>.</summary>
internal sealed class AccessUserMaintenance : IAccessUserMaintenance
{
    private readonly AccessDbContext _db;
    private readonly OidcClientRegistry _clients;

    public AccessUserMaintenance(AccessDbContext db, OidcClientRegistry clients)
    {
        _db = db;
        _clients = clients;
    }

    public async Task ForgetUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        _db.UserGrants.RemoveRange(await _db.UserGrants
            .Where(grant => grant.UserId == userId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false));

        _db.GroupMembers.RemoveRange(await _db.GroupMembers
            .Where(member => member.UserId == userId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false));

        _db.AccessRequests.RemoveRange(await _db.AccessRequests
            .Where(request => request.UserId == userId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false));

        _db.OrganizationMembers.RemoveRange(await _db.OrganizationMembers
            .Where(member => member.UserId == userId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false));

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Nothing grants the account anything any more; its tokens go in every application at once.
        await RevokeTokensAsync(userId, cancellationToken).ConfigureAwait(false);
    }

    public Task RevokeTokensAsync(Guid userId, CancellationToken cancellationToken) =>
        _clients.RevokeUserAsync(userId, cancellationToken);
}
