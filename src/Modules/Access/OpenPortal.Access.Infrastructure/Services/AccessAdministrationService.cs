using Microsoft.EntityFrameworkCore;
using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Access.Application.Contracts;
using OpenPortal.Access.Domain;
using OpenPortal.Access.Domain.Applications;
using OpenPortal.Access.Domain.Grants;
using OpenPortal.Access.Infrastructure.Persistence;
using OpenPortal.SharedKernel.Results;
using OpenPortal.SharedKernel.Time;

namespace OpenPortal.Access.Infrastructure.Services;

/// <summary>Grants and the views that explain them.</summary>
internal sealed class AccessAdministrationService : IAccessAdministrationService
{
    private readonly AccessDbContext _db;
    private readonly AccessQueries _access;
    private readonly IUserDirectory _directory;
    private readonly IAccessAdminAuthorization _authorization;
    private readonly IClock _clock;

    public AccessAdministrationService(
        AccessDbContext db,
        AccessQueries access,
        IUserDirectory directory,
        IAccessAdminAuthorization authorization,
        IClock clock)
    {
        _db = db;
        _access = access;
        _directory = directory;
        _authorization = authorization;
        _clock = clock;
    }

    public async Task<Result<AccessTreeDto>> GetTreeAsync(CancellationToken cancellationToken)
    {
        var guard = await _authorization.EnsureCanAsync(AccessOperation.ViewTree, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<AccessTreeDto>.Failure(guard.Error);
        }

        // Four flat reads assembled in memory: the tree is an administration view over a modest number of
        // rows, and one query per node would grow with every application added.
        var applications = await _db.Applications.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        var groups = await _db.Groups.AsNoTracking().Include(group => group.Members).ToListAsync(cancellationToken).ConfigureAwait(false);
        var userGrants = await _db.UserGrants.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        var groupGrants = await _db.GroupGrants.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);

        var resolve = await AccessQueries.ResolveUsersAsync(
                _directory,
                userGrants.Select(grant => grant.UserId)
                    .Concat(groups.SelectMany(group => group.Members).Select(member => member.UserId)),
                cancellationToken)
            .ConfigureAwait(false);

        var groupsById = groups.ToDictionary(group => group.Id);

        var tree = applications
            .OrderBy(application => application.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(application => new AccessTreeApplicationDto(
                application.Id,
                application.ClientId,
                application.DisplayName,
                application.Status.ToContract(),
                application.LastSeenAtUtc,
                groupGrants
                    .Where(grant => grant.ApplicationId == application.Id)
                    .Select(grant => groupsById[grant.GroupId])
                    .OrderBy(group => group.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(group => new AccessTreeGroupDto(
                        group.Id,
                        group.Name,
                        group.Members
                            .Select(member => resolve(member.UserId))
                            .OrderBy(user => user.DisplayName, StringComparer.OrdinalIgnoreCase)
                            .ToList()))
                    .ToList(),
                userGrants
                    .Where(grant => grant.ApplicationId == application.Id)
                    .Select(grant => resolve(grant.UserId))
                    .OrderBy(user => user.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ToList()))
            .ToList();

        return Result<AccessTreeDto>.Success(new AccessTreeDto(tree));
    }

    public async Task<Result<UserAccessDto>> GetUserAccessAsync(Guid userId, CancellationToken cancellationToken)
    {
        var guard = await _authorization.EnsureCanAsync(AccessOperation.ViewUserAccess, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<UserAccessDto>.Failure(guard.Error);
        }

        var memberships = await _db.GroupMembers
            .AsNoTracking()
            .Where(member => member.UserId == userId)
            .Join(_db.Groups, member => member.GroupId, group => group.Id, (_, group) => new GroupReferenceDto(group.Id, group.Name))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var groupIds = memberships.Select(group => group.Id).ToArray();

        var directIds = await _db.UserGrants
            .Where(grant => grant.UserId == userId)
            .Select(grant => grant.ApplicationId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var viaGroups = await _db.GroupGrants
            .Where(grant => groupIds.Contains(grant.GroupId))
            .Select(grant => new { grant.ApplicationId, grant.GroupId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var applicationIds = directIds.Concat(viaGroups.Select(grant => grant.ApplicationId)).Distinct().ToArray();

        var applications = await _db.Applications
            .AsNoTracking()
            .Where(application => applicationIds.Contains(application.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var groupNames = memberships.ToDictionary(group => group.Id);

        var access = applications
            .OrderBy(application => application.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(application => new UserApplicationAccessDto(
                application.Id,
                application.ClientId,
                application.DisplayName,
                application.Status.ToContract(),
                directIds.Contains(application.Id),
                viaGroups
                    .Where(grant => grant.ApplicationId == application.Id)
                    .Select(grant => groupNames[grant.GroupId])
                    .OrderBy(group => group.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList()))
            .ToList();

        return Result<UserAccessDto>.Success(new UserAccessDto(
            userId,
            memberships.OrderBy(group => group.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            access));
    }

    public async Task<Result> GrantUserAsync(Guid applicationId, Guid userId, CancellationToken cancellationToken)
    {
        var guard = await _authorization.EnsureCanAsync(AccessOperation.ManageUserGrants, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return guard;
        }

        if (!await ApplicationExistsAsync(applicationId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(AccessErrors.ApplicationNotFound);
        }

        var users = await _directory.FindAsync([userId], cancellationToken).ConfigureAwait(false);
        if (!users.ContainsKey(userId))
        {
            return Result.Failure(AccessErrors.UserNotFound);
        }

        var exists = await _db.UserGrants
            .AnyAsync(grant => grant.ApplicationId == applicationId && grant.UserId == userId, cancellationToken)
            .ConfigureAwait(false);

        if (!exists)
        {
            _db.UserGrants.Add(new ApplicationUserGrant(applicationId, userId, _clock.UtcNow));
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return Result.Success();
    }

    public async Task<Result> RevokeUserAsync(Guid applicationId, Guid userId, CancellationToken cancellationToken)
    {
        var guard = await _authorization.EnsureCanAsync(AccessOperation.ManageUserGrants, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return guard;
        }

        var application = await FindApplicationAsync(applicationId, cancellationToken).ConfigureAwait(false);
        if (application is null)
        {
            return Result.Failure(AccessErrors.ApplicationNotFound);
        }

        var grant = await _db.UserGrants
            .SingleOrDefaultAsync(candidate => candidate.ApplicationId == applicationId && candidate.UserId == userId, cancellationToken)
            .ConfigureAwait(false);

        if (grant is not null)
        {
            _db.UserGrants.Remove(grant);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await _access.RevokeIfNoLongerAllowedAsync(application, [userId], cancellationToken).ConfigureAwait(false);
        }

        return Result.Success();
    }

    public async Task<Result> GrantGroupAsync(Guid applicationId, Guid groupId, CancellationToken cancellationToken)
    {
        var guard = await _authorization.EnsureCanAsync(AccessOperation.ManageGroupGrants, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return guard;
        }

        if (!await ApplicationExistsAsync(applicationId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(AccessErrors.ApplicationNotFound);
        }

        if (!await _db.Groups.AnyAsync(group => group.Id == groupId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(AccessErrors.GroupNotFound);
        }

        var exists = await _db.GroupGrants
            .AnyAsync(grant => grant.ApplicationId == applicationId && grant.GroupId == groupId, cancellationToken)
            .ConfigureAwait(false);

        if (!exists)
        {
            _db.GroupGrants.Add(new ApplicationGroupGrant(applicationId, groupId, _clock.UtcNow));
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return Result.Success();
    }

    public async Task<Result> RevokeGroupAsync(Guid applicationId, Guid groupId, CancellationToken cancellationToken)
    {
        var guard = await _authorization.EnsureCanAsync(AccessOperation.ManageGroupGrants, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return guard;
        }

        var application = await FindApplicationAsync(applicationId, cancellationToken).ConfigureAwait(false);
        if (application is null)
        {
            return Result.Failure(AccessErrors.ApplicationNotFound);
        }

        var grant = await _db.GroupGrants
            .SingleOrDefaultAsync(candidate => candidate.ApplicationId == applicationId && candidate.GroupId == groupId, cancellationToken)
            .ConfigureAwait(false);

        if (grant is not null)
        {
            var members = await _db.GroupMembers
                .Where(member => member.GroupId == groupId)
                .Select(member => member.UserId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            _db.GroupGrants.Remove(grant);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await _access.RevokeIfNoLongerAllowedAsync(application, members, cancellationToken).ConfigureAwait(false);
        }

        return Result.Success();
    }

    public async Task<Result> RemoveUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var guard = await _authorization.EnsureCanAsync(AccessOperation.ManageUserGrants, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return guard;
        }

        var applicationIds = await _access.GrantedApplicationIdsAsync(userId, cancellationToken).ConfigureAwait(false);

        _db.UserGrants.RemoveRange(await _db.UserGrants
            .Where(grant => grant.UserId == userId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false));

        _db.GroupMembers.RemoveRange(await _db.GroupMembers
            .Where(member => member.UserId == userId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false));

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Nothing grants the user anything any more, so this revokes every token they held.
        var applications = await _db.Applications
            .AsNoTracking()
            .Where(application => applicationIds.Contains(application.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var application in applications)
        {
            await _access.RevokeIfNoLongerAllowedAsync(application, [userId], cancellationToken).ConfigureAwait(false);
        }

        return Result.Success();
    }

    private Task<bool> ApplicationExistsAsync(Guid applicationId, CancellationToken cancellationToken) =>
        _db.Applications.AnyAsync(application => application.Id == applicationId, cancellationToken);

    private Task<PortalApplication?> FindApplicationAsync(Guid applicationId, CancellationToken cancellationToken) =>
        _db.Applications.AsNoTracking().SingleOrDefaultAsync(application => application.Id == applicationId, cancellationToken);
}
