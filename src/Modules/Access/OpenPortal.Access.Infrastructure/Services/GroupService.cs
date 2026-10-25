using Microsoft.EntityFrameworkCore;
using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Access.Application.Auditing;
using OpenPortal.Access.Application.Contracts;
using OpenPortal.Access.Domain;
using OpenPortal.Access.Domain.Groups;
using OpenPortal.Access.Infrastructure.Persistence;
using OpenPortal.Access.Application.Notifications;
using OpenPortal.SharedKernel.Auditing;
using OpenPortal.SharedKernel.Notifications;
using OpenPortal.SharedKernel.Results;
using OpenPortal.SharedKernel.Time;

namespace OpenPortal.Access.Infrastructure.Services;

/// <summary>Administration of user groups and their membership.</summary>
internal sealed class GroupService : IGroupService
{
    private readonly AccessDbContext _db;
    private readonly AccessQueries _access;
    private readonly IUserDirectory _directory;
    private readonly IAccessAdminAuthorization _authorization;
    private readonly IPortalPageCatalog _catalog;
    private readonly IClock _clock;
    private readonly IAuditTrail _audit;
    private readonly INotifier _notifier;
    private readonly GroupGraph _graph;

    public GroupService(
        AccessDbContext db,
        AccessQueries access,
        IUserDirectory directory,
        IAccessAdminAuthorization authorization,
        IPortalPageCatalog catalog,
        IClock clock,
        IAuditTrail audit,
        INotifier notifier,
        GroupGraph graph)
    {
        _graph = graph;
        _audit = audit;
        _notifier = notifier;
        _db = db;
        _access = access;
        _directory = directory;
        _authorization = authorization;
        _catalog = catalog;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyList<GroupSummaryDto>>> ListAsync(CancellationToken cancellationToken)
    {
        var slice = await ListAsync(ListSlice.All, cancellationToken).ConfigureAwait(false);

        return slice.IsSuccess
            ? Result<IReadOnlyList<GroupSummaryDto>>.Success(slice.Value.Items)
            : Result<IReadOnlyList<GroupSummaryDto>>.Failure(slice.Error);
    }

    public async Task<Result<SliceDto<GroupSummaryDto>>> ListAsync(ListSlice slice, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(slice);

        var guard = await _authorization.EnsureCanAsync(AccessOperation.ListGroups, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<SliceDto<GroupSummaryDto>>.Failure(guard.Error);
        }

        var query = _db.Groups.AsNoTracking();

        // Compared upper-cased on both sides: LIKE is case-sensitive on one engine and not on the other.
        var term = slice.Search?.Trim().ToUpperInvariant();
        if (!string.IsNullOrEmpty(term))
        {
            query = query.Where(group => group.Name.ToUpper().Contains(term)
                || (group.Description != null && group.Description.ToUpper().Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        query = query.OrderBy(group => group.Name).Skip(Math.Max(0, slice.Skip));
        if (slice.Take is int take)
        {
            query = query.Take(Math.Clamp(take, 1, ListSlice.MaxTake));
        }

        var groups = await query
            .Select(group => new
            {
                group.Id,
                group.Name,
                group.Description,
                group.CreatedAtUtc,
                MemberCount = group.Members.Count,
                ApplicationCount = _db.GroupGrants.Count(grant => grant.GroupId == group.Id),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<GroupSummaryDto> result = groups
            .OrderBy(group => group.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => new GroupSummaryDto(
                group.Id,
                group.Name,
                group.Description,
                group.MemberCount,
                group.ApplicationCount,
                group.CreatedAtUtc))
            .ToList();

        return Result<SliceDto<GroupSummaryDto>>.Success(new SliceDto<GroupSummaryDto>(result, total));
    }

    public async Task<Result<GroupDetailDto>> GetAsync(Guid groupId, CancellationToken cancellationToken)
    {
        var guard = await _authorization.EnsureCanAsync(AccessOperation.ManageGroups, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<GroupDetailDto>.Failure(guard.Error);
        }

        var group = await FindAsync(groupId, cancellationToken).ConfigureAwait(false);

        return group is null
            ? Result<GroupDetailDto>.Failure(AccessErrors.GroupNotFound)
            : Result<GroupDetailDto>.Success(await ToDetailAsync(group, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<GroupDetailDto>> CreateAsync(SaveGroupRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var guard = await _authorization.EnsureCanAsync(AccessOperation.ManageGroups, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<GroupDetailDto>.Failure(guard.Error);
        }

        var created = Group.Create(Guid.NewGuid(), request.Name, request.Description, _clock.UtcNow);
        if (created.IsFailure)
        {
            return Result<GroupDetailDto>.Failure(created.Error);
        }

        if (await NameInUseAsync(created.Value.NormalisedName, exceptId: null, cancellationToken).ConfigureAwait(false))
        {
            return Result<GroupDetailDto>.Failure(AccessErrors.GroupNameInUse);
        }

        _db.Groups.Add(created.Value);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _audit.RecordAsync(AuditEvent.Succeeded(AccessAuditActions.GroupCreated, created.Value.ToAuditSubject()), cancellationToken)
            .ConfigureAwait(false);

        return Result<GroupDetailDto>.Success(await ToDetailAsync(created.Value, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<GroupDetailDto>> UpdateAsync(
        Guid groupId,
        SaveGroupRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var guard = await _authorization.EnsureCanAsync(AccessOperation.ManageGroups, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<GroupDetailDto>.Failure(guard.Error);
        }

        var group = await FindAsync(groupId, cancellationToken).ConfigureAwait(false);
        if (group is null)
        {
            return Result<GroupDetailDto>.Failure(AccessErrors.GroupNotFound);
        }

        var name = Group.ValidateName(request.Name);
        if (name.IsSuccess
            && await NameInUseAsync(name.Value.ToUpperInvariant(), group.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result<GroupDetailDto>.Failure(AccessErrors.GroupNameInUse);
        }

        var updated = group.Update(request.Name, request.Description, _clock.UtcNow);
        if (updated.IsFailure)
        {
            return Result<GroupDetailDto>.Failure(updated.Error);
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _audit.RecordAsync(AuditEvent.Succeeded(AccessAuditActions.GroupUpdated, group.ToAuditSubject()), cancellationToken)
            .ConfigureAwait(false);

        return Result<GroupDetailDto>.Success(await ToDetailAsync(group, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result> DeleteAsync(Guid groupId, CancellationToken cancellationToken)
    {
        var guard = await _authorization.EnsureCanAsync(AccessOperation.ManageGroups, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return guard;
        }

        var group = await FindAsync(groupId, cancellationToken).ConfigureAwait(false);
        if (group is null)
        {
            return Result.Failure(AccessErrors.GroupNotFound);
        }

        // Everyone in the group or its subgroups loses what the group, and the groups above it, gave them.
        var memberIds = (await _graph.MembersOfGroupAsync(group.Id, cancellationToken).ConfigureAwait(false)).ToArray();
        var applications = await GrantedApplicationsAsync(
                await _graph.AncestorsAndSelfAsync([group.Id], cancellationToken).ConfigureAwait(false),
                cancellationToken)
            .ConfigureAwait(false);

        // Members and grants go with the group through the cascading foreign keys.
        _db.Groups.Remove(group);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        foreach (var application in applications)
        {
            await _access.RevokeIfNoLongerAllowedAsync(application, memberIds, cancellationToken).ConfigureAwait(false);
        }

        await _audit.RecordAsync(
                AuditEvent.Succeeded(
                    AccessAuditActions.GroupDeleted,
                    group.ToAuditSubject(),
                    AccessAudit.Details(("members", memberIds.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)))),
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success();
    }

    public async Task<Result<GroupDetailDto>> AddMemberAsync(
        Guid groupId,
        Guid userId,
        CancellationToken cancellationToken,
        DateTimeOffset? expiresAtUtc = null)
    {
        if (expiresAtUtc is { } end && end <= _clock.UtcNow)
        {
            return Result<GroupDetailDto>.Failure(AccessErrors.ExpiryInPast);
        }

        var guard = await _authorization.EnsureCanAsync(AccessOperation.ManageGroups, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<GroupDetailDto>.Failure(guard.Error);
        }

        var group = await FindAsync(groupId, cancellationToken).ConfigureAwait(false);
        if (group is null)
        {
            return Result<GroupDetailDto>.Failure(AccessErrors.GroupNotFound);
        }

        var users = await _directory.FindAsync([userId], cancellationToken).ConfigureAwait(false);
        if (!users.TryGetValue(userId, out var user))
        {
            return Result<GroupDetailDto>.Failure(AccessErrors.UserNotFound);
        }

        var wasMember = group.Members.Any(member => member.UserId == userId);

        if (group.AddMember(userId, _clock.UtcNow, expiresAtUtc) && wasMember)
        {
            // Only the end of the membership changed.
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        else if (!wasMember)
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await _audit.RecordAsync(
                    AuditEvent.Succeeded(
                        AccessAuditActions.GroupMemberAdded,
                        new AuditSubject(AuditSubjectTypes.User, userId.ToString(), user.Email),
                        AccessAudit.Details(("group", group.Name), ("groupId", group.Id.ToString()))),
                    cancellationToken)
                .ConfigureAwait(false);

            await _notifier.NotifyAsync(
                    new Notice
                    {
                        Type = AccessNotificationTypes.GroupJoined,
                        Audience = NoticeAudience.Users(userId),
                        Parameters = new Dictionary<string, string?> { ["group"] = group.Name },
                        Link = "/",
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return Result<GroupDetailDto>.Success(await ToDetailAsync(group, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<GroupDetailDto>> RemoveMemberAsync(
        Guid groupId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var guard = await _authorization.EnsureCanAsync(AccessOperation.ManageGroups, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<GroupDetailDto>.Failure(guard.Error);
        }

        var group = await FindAsync(groupId, cancellationToken).ConfigureAwait(false);
        if (group is null)
        {
            return Result<GroupDetailDto>.Failure(AccessErrors.GroupNotFound);
        }

        if (group.RemoveMember(userId))
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            var groups = await _graph.AncestorsAndSelfAsync([group.Id], cancellationToken).ConfigureAwait(false);
            foreach (var application in await GrantedApplicationsAsync(groups, cancellationToken).ConfigureAwait(false))
            {
                await _access.RevokeIfNoLongerAllowedAsync(application, [userId], cancellationToken).ConfigureAwait(false);
            }

            await _audit.RecordAsync(
                    AuditEvent.Succeeded(
                        AccessAuditActions.GroupMemberRemoved,
                        await _directory.UserSubjectAsync(userId, cancellationToken).ConfigureAwait(false),
                        AccessAudit.Details(("group", group.Name), ("groupId", group.Id.ToString()))),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return Result<GroupDetailDto>.Success(await ToDetailAsync(group, cancellationToken).ConfigureAwait(false));
    }

    private Task<Group?> FindAsync(Guid groupId, CancellationToken cancellationToken) =>
        _db.Groups
            .Include(group => group.Members)
            .SingleOrDefaultAsync(group => group.Id == groupId, cancellationToken);

    private Task<bool> NameInUseAsync(string normalisedName, Guid? exceptId, CancellationToken cancellationToken) =>
        _db.Groups.AnyAsync(
            group => group.NormalisedName == normalisedName && (exceptId == null || group.Id != exceptId),
            cancellationToken);

    public async Task<Result<GroupDetailDto>> AddSubgroupAsync(Guid groupId, Guid childGroupId, CancellationToken cancellationToken)
    {
        var guard = await _authorization.EnsureCanAsync(AccessOperation.ManageGroups, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<GroupDetailDto>.Failure(guard.Error);
        }

        var group = await FindAsync(groupId, cancellationToken).ConfigureAwait(false);
        var child = await _db.Groups.AsNoTracking().SingleOrDefaultAsync(candidate => candidate.Id == childGroupId, cancellationToken).ConfigureAwait(false);
        if (group is null || child is null)
        {
            return Result<GroupDetailDto>.Failure(AccessErrors.GroupNotFound);
        }

        if (await _graph.WouldCycleAsync(groupId, childGroupId, cancellationToken).ConfigureAwait(false))
        {
            return Result<GroupDetailDto>.Failure(AccessErrors.GroupNestingCycle);
        }

        var exists = await _db.GroupNestings
            .AnyAsync(nesting => nesting.ParentGroupId == groupId && nesting.ChildGroupId == childGroupId, cancellationToken)
            .ConfigureAwait(false);

        if (!exists)
        {
            _db.GroupNestings.Add(new GroupNesting(groupId, childGroupId, _clock.UtcNow));
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _graph.Invalidate();

            await _audit.RecordAsync(
                    AuditEvent.Succeeded(
                        AccessAuditActions.SubgroupAdded,
                        group.ToAuditSubject(),
                        AccessAudit.Details(("subgroup", child.Name), ("subgroupId", child.Id.ToString()))),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return Result<GroupDetailDto>.Success(await ToDetailAsync(group, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<GroupDetailDto>> RemoveSubgroupAsync(Guid groupId, Guid childGroupId, CancellationToken cancellationToken)
    {
        var guard = await _authorization.EnsureCanAsync(AccessOperation.ManageGroups, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<GroupDetailDto>.Failure(guard.Error);
        }

        var group = await FindAsync(groupId, cancellationToken).ConfigureAwait(false);
        if (group is null)
        {
            return Result<GroupDetailDto>.Failure(AccessErrors.GroupNotFound);
        }

        var nesting = await _db.GroupNestings
            .SingleOrDefaultAsync(candidate => candidate.ParentGroupId == groupId && candidate.ChildGroupId == childGroupId, cancellationToken)
            .ConfigureAwait(false);

        if (nesting is not null)
        {
            // Read before the edge goes: who was reaching the parent's grants through the subgroup.
            var members = await _graph.MembersOfGroupAsync(childGroupId, cancellationToken).ConfigureAwait(false);
            var applications = await GrantedApplicationsAsync(
                    await _graph.AncestorsAndSelfAsync([groupId], cancellationToken).ConfigureAwait(false),
                    cancellationToken)
                .ConfigureAwait(false);

            _db.GroupNestings.Remove(nesting);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _graph.Invalidate();

            foreach (var application in applications)
            {
                await _access.RevokeIfNoLongerAllowedAsync(application, members, cancellationToken).ConfigureAwait(false);
            }

            var childName = await _db.Groups.Where(candidate => candidate.Id == childGroupId).Select(candidate => candidate.Name)
                .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);

            await _audit.RecordAsync(
                    AuditEvent.Succeeded(
                        AccessAuditActions.SubgroupRemoved,
                        group.ToAuditSubject(),
                        AccessAudit.Details(("subgroup", childName), ("subgroupId", childGroupId.ToString()))),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return Result<GroupDetailDto>.Success(await ToDetailAsync(group, cancellationToken).ConfigureAwait(false));
    }

    private Task<List<Domain.Applications.PortalApplication>> GrantedApplicationsAsync(
        Guid groupId,
        CancellationToken cancellationToken) =>
        GrantedApplicationsAsync([groupId], cancellationToken);

    private Task<List<Domain.Applications.PortalApplication>> GrantedApplicationsAsync(
        IReadOnlyCollection<Guid> groupIds,
        CancellationToken cancellationToken) =>
        _db.GroupGrants
            .Where(grant => groupIds.Contains(grant.GroupId))
            .Select(grant => grant.ApplicationId)
            .Distinct()
            .Join(_db.Applications, id => id, application => application.Id, (_, application) => application)
            .ToListAsync(cancellationToken);

    private async Task<GroupDetailDto> ToDetailAsync(Group group, CancellationToken cancellationToken)
    {
        var resolve = await AccessQueries
            .ResolveUsersAsync(_directory, group.Members.Select(member => member.UserId), cancellationToken)
            .ConfigureAwait(false);

        var applications = await GrantedApplicationsAsync(group.Id, cancellationToken).ConfigureAwait(false);

        var pages = await _db.PageGrants
            .AsNoTracking()
            .Where(grant => grant.GroupId == group.Id)
            .Select(grant => grant.PageKey)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new GroupDetailDto(
            group.Id,
            group.Name,
            group.Description,
            group.Members
                .Select(member => resolve(member.UserId))
                .OrderBy(user => user.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            applications
                .OrderBy(application => application.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(application => application.ToReference())
                .ToList(),
            pages.Where(_catalog.IsKnown).Order(StringComparer.Ordinal).ToList(),
            group.CreatedAtUtc,
            await NestedAsync(group.Id, children: true, cancellationToken).ConfigureAwait(false),
            await NestedAsync(group.Id, children: false, cancellationToken).ConfigureAwait(false),
            group.Members
                .Where(member => member.ExpiresAtUtc is not null)
                .ToDictionary(member => member.UserId, member => member.ExpiresAtUtc!.Value));
    }

    /// <summary>The groups directly inside the group (<paramref name="children"/>) or directly above it.</summary>
    private async Task<IReadOnlyList<GroupReferenceDto>> NestedAsync(Guid groupId, bool children, CancellationToken cancellationToken)
    {
        var ids = children
            ? _db.GroupNestings.Where(nesting => nesting.ParentGroupId == groupId).Select(nesting => nesting.ChildGroupId)
            : _db.GroupNestings.Where(nesting => nesting.ChildGroupId == groupId).Select(nesting => nesting.ParentGroupId);

        var groups = await _db.Groups
            .AsNoTracking()
            .Where(group => ids.Contains(group.Id))
            .Select(group => new GroupReferenceDto(group.Id, group.Name))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return groups.OrderBy(group => group.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
