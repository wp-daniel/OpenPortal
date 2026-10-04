using Microsoft.EntityFrameworkCore;
using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Access.Application.Contracts;
using OpenPortal.Access.Domain.Applications;
using OpenPortal.Access.Infrastructure.Persistence;

namespace OpenPortal.Access.Infrastructure.Services;

/// <summary>
/// The access rule, written once: a user may open an application when it is active and the user holds a
/// direct grant or belongs to a group that holds one. Every reader and every revocation goes through here,
/// so the sign-in check and the administration screens cannot disagree.
/// </summary>
internal sealed class AccessQueries
{
    private readonly AccessDbContext _db;
    private readonly OidcClientRegistry _clients;

    public AccessQueries(AccessDbContext db, OidcClientRegistry clients)
    {
        _db = db;
        _clients = clients;
    }

    /// <summary>Whether the user holds a grant on the application, ignoring the application's status.</summary>
    public async Task<bool> HasGrantAsync(Guid applicationId, Guid userId, CancellationToken cancellationToken)
    {
        var direct = await _db.UserGrants
            .AnyAsync(grant => grant.ApplicationId == applicationId && grant.UserId == userId, cancellationToken)
            .ConfigureAwait(false);

        if (direct)
        {
            return true;
        }

        return await _db.GroupGrants
            .Where(grant => grant.ApplicationId == applicationId)
            .Join(_db.GroupMembers, grant => grant.GroupId, member => member.GroupId, (_, member) => member)
            .AnyAsync(member => member.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Ids of the applications the user holds a grant on, directly or through a group.</summary>
    public async Task<HashSet<Guid>> GrantedApplicationIdsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var direct = await _db.UserGrants
            .Where(grant => grant.UserId == userId)
            .Select(grant => grant.ApplicationId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var viaGroups = await _db.GroupMembers
            .Where(member => member.UserId == userId)
            .Join(_db.GroupGrants, member => member.GroupId, grant => grant.GroupId, (_, grant) => grant.ApplicationId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. direct, .. viaGroups];
    }

    /// <summary>
    /// After a grant or membership was removed, revokes the tokens of each user who no longer holds any
    /// grant on the application. Users who still have access another way keep their session.
    /// </summary>
    public async Task RevokeIfNoLongerAllowedAsync(
        PortalApplication application,
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken)
    {
        var lost = new List<Guid>();

        foreach (var userId in userIds.Distinct())
        {
            if (!await HasGrantAsync(application.Id, userId, cancellationToken).ConfigureAwait(false))
            {
                lost.Add(userId);
            }
        }

        if (lost.Count > 0)
        {
            await _clients.RevokeAsync(application.ClientId, lost, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Resolves user ids to references, marking accounts that no longer exist.</summary>
    public static async Task<Func<Guid, UserReferenceDto>> ResolveUsersAsync(
        IUserDirectory directory,
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken)
    {
        var ids = userIds.Distinct().ToArray();
        var found = ids.Length == 0
            ? new Dictionary<Guid, DirectoryUser>()
            : await directory.FindAsync(ids, cancellationToken).ConfigureAwait(false);

        return id => found.TryGetValue(id, out var user)
            ? new UserReferenceDto(user.Id, user.Email, user.DisplayName, IsKnown: true)
            : new UserReferenceDto(id, string.Empty, string.Empty, IsKnown: false);
    }
}

/// <summary>Entity-to-contract mapping shared by the Access services.</summary>
internal static class AccessMapping
{
    public static string ToContract(this ApplicationStatus status) => status switch
    {
        ApplicationStatus.Pending => "pending",
        ApplicationStatus.Active => "active",
        ApplicationStatus.Disabled => "disabled",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    public static string ToContract(this ApplicationSource source) => source switch
    {
        ApplicationSource.Manual => "manual",
        ApplicationSource.Announced => "announced",
        _ => throw new ArgumentOutOfRangeException(nameof(source), source, null),
    };

    public static ApplicationDto ToDto(this PortalApplication application, int userCount, int groupCount) => new(
        application.Id,
        application.ClientId,
        application.DisplayName,
        application.Description,
        application.BaseUrl,
        application.RedirectUris,
        application.PostLogoutRedirectUris,
        application.Status.ToContract(),
        application.Source.ToContract(),
        application.Version,
        application.AnnouncedRedirectUris,
        application.AnnouncedPostLogoutRedirectUris,
        application.HasManifestChanges,
        application.CreatedAtUtc,
        application.UpdatedAtUtc,
        application.LastSeenAtUtc,
        userCount,
        groupCount);

    public static ApplicationReferenceDto ToReference(this PortalApplication application) =>
        new(application.Id, application.ClientId, application.DisplayName, application.Status.ToContract());
}
