using Microsoft.EntityFrameworkCore;
using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Access.Application.Auditing;
using OpenPortal.Access.Application.Contracts;
using OpenPortal.Access.Domain;
using OpenPortal.Access.Domain.Groups;
using OpenPortal.Access.Infrastructure.Persistence;
using OpenPortal.SharedKernel.Auditing;
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

    public GroupService(
        AccessDbContext db,
        AccessQueries access,
        IUserDirectory directory,
        IAccessAdminAuthorization authorization,
        IPortalPageCatalog catalog,
        IClock clock,
        IAuditTrail audit)
    {
        _audit = audit;
        _db = db;
        _access = access;
        _directory = directory;
        _authorization = authorization;
        _catalog = catalog;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyList<GroupSummaryDto>>> ListAsync(CancellationToken cancellationToken)
    {
        var guard = await _authorization.EnsureCanAsync(AccessOperation.ListGroups, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<IReadOnlyList<GroupSummaryDto>>.Failure(guard.Error);
        }

        var groups = await _db.Groups
            .AsNoTracking()
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

        return Result<IReadOnlyList<GroupSummaryDto>>.Success(result);
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

        var memberIds = group.Members.Select(member => member.UserId).ToArray();
        var applications = await GrantedApplicationsAsync(group.Id, cancellationToken).ConfigureAwait(false);

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

    public async Task<Result<GroupDetailDto>> AddMemberAsync(Guid groupId, Guid userId, CancellationToken cancellationToken)
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

        var users = await _directory.FindAsync([userId], cancellationToken).ConfigureAwait(false);
        if (!users.TryGetValue(userId, out var user))
        {
            return Result<GroupDetailDto>.Failure(AccessErrors.UserNotFound);
        }

        if (group.AddMember(userId, _clock.UtcNow))
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await _audit.RecordAsync(
                    AuditEvent.Succeeded(
                        AccessAuditActions.GroupMemberAdded,
                        new AuditSubject(AuditSubjectTypes.User, userId.ToString(), user.Email),
                        AccessAudit.Details(("group", group.Name), ("groupId", group.Id.ToString()))),
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

            foreach (var application in await GrantedApplicationsAsync(group.Id, cancellationToken).ConfigureAwait(false))
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

    private Task<List<Domain.Applications.PortalApplication>> GrantedApplicationsAsync(
        Guid groupId,
        CancellationToken cancellationToken) =>
        _db.GroupGrants
            .Where(grant => grant.GroupId == groupId)
            .Join(_db.Applications, grant => grant.ApplicationId, application => application.Id, (_, application) => application)
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
            group.CreatedAtUtc);
    }
}
