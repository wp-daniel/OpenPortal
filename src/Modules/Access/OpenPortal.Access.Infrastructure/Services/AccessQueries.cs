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
    /// The user's roles in the application: those on their own grant and on the grants of every group they
    /// belong to, limited to the roles the application still defines.
    /// </summary>
    public async Task<IReadOnlyList<string>> RolesAsync(PortalApplication application, Guid userId, CancellationToken cancellationToken)
    {
        if (application.Roles.Count == 0)
        {
            return [];
        }

        var direct = await _db.UserGrants
            .AsNoTracking()
            .Where(grant => grant.ApplicationId == application.Id && grant.UserId == userId)
            .Select(grant => grant.Roles)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var viaGroups = await _db.GroupGrants
            .AsNoTracking()
            .Where(grant => grant.ApplicationId == application.Id)
            .Join(_db.GroupMembers, grant => grant.GroupId, member => member.GroupId, (grant, member) => new { grant.Roles, member.UserId })
            .Where(row => row.UserId == userId)
            .Select(row => row.Roles)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return direct.Concat(viaGroups)
            .SelectMany(roles => roles)
            .Where(application.HasRole)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>The group names the application receives for the user, as its group claim setting allows.</summary>
    public async Task<IReadOnlyList<string>> GroupClaimsAsync(PortalApplication application, Guid userId, CancellationToken cancellationToken)
    {
        if (application.GroupClaims == GroupClaimMode.None)
        {
            return [];
        }

        var groups = _db.GroupMembers
            .Where(member => member.UserId == userId)
            .Join(_db.Groups, member => member.GroupId, group => group.Id, (_, group) => group);

        if (application.GroupClaims == GroupClaimMode.Granted)
        {
            groups = groups.Where(group => _db.GroupGrants.Any(grant => grant.ApplicationId == application.Id && grant.GroupId == group.Id));
        }

        var names = await groups.Select(group => group.Name).ToListAsync(cancellationToken).ConfigureAwait(false);

        return names.Order(StringComparer.Ordinal).ToList();
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

    public static string ToContract(this GroupClaimMode mode) => mode switch
    {
        GroupClaimMode.None => GroupClaimModes.None,
        GroupClaimMode.Granted => GroupClaimModes.Granted,
        GroupClaimMode.All => GroupClaimModes.All,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

    /// <summary>Reads a group claim setting from a request; null for anything unknown.</summary>
    public static GroupClaimMode? ParseGroupClaims(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        GroupClaimModes.None => GroupClaimMode.None,
        GroupClaimModes.Granted => GroupClaimMode.Granted,
        GroupClaimModes.All => GroupClaimMode.All,
        _ => null,
    };

    public static IReadOnlyList<ApplicationRoleDto> RolesToContract(this PortalApplication application) =>
        application.Roles
            .OrderBy(role => role.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(role => new ApplicationRoleDto(role.Key, role.DisplayName, role.Description))
            .ToList();

    public static IEnumerable<ApplicationRoleDetails> ToDetails(this IEnumerable<ApplicationRoleDto> roles) =>
        roles.Select(role => new ApplicationRoleDetails(role.Key, role.DisplayName, role.Description));

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
        groupCount,
        application.RolesToContract(),
        application.GroupClaims.ToContract());

    public static ApplicationReferenceDto ToReference(this PortalApplication application) =>
        new(application.Id, application.ClientId, application.DisplayName, application.Status.ToContract());
}
