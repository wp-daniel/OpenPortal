using Microsoft.EntityFrameworkCore;
using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Access.Application.Auditing;
using OpenPortal.Access.Application.Contracts;
using OpenPortal.Access.Domain;
using OpenPortal.Access.Domain.Applications;
using OpenPortal.Access.Domain.Grants;
using OpenPortal.Access.Infrastructure.Persistence;
using OpenPortal.SharedKernel.Auditing;
using OpenPortal.SharedKernel.Results;
using OpenPortal.SharedKernel.Time;

namespace OpenPortal.Access.Infrastructure.Services;

/// <summary>Grants, the roles that come with them, and the views that explain them.</summary>
internal sealed class AccessAdministrationService : IAccessAdministrationService
{
    private readonly AccessDbContext _db;
    private readonly AccessQueries _access;
    private readonly IUserDirectory _directory;
    private readonly IAccessAdminAuthorization _authorization;
    private readonly IClock _clock;
    private readonly IAuditTrail _audit;
    private readonly UserGrantOperations _grants;
    private readonly IAccessUserMaintenance _maintenance;
    private readonly GroupGraph _graph;
    private readonly IAccessCaller _caller;
    private readonly IAccessEvaluator _evaluator;

    public AccessAdministrationService(
        AccessDbContext db,
        AccessQueries access,
        IUserDirectory directory,
        IAccessAdminAuthorization authorization,
        IClock clock,
        IAuditTrail audit,
        UserGrantOperations grants,
        IAccessUserMaintenance maintenance,
        GroupGraph graph,
        IAccessCaller caller,
        IAccessEvaluator evaluator)
    {
        _graph = graph;
        _db = db;
        _access = access;
        _directory = directory;
        _authorization = authorization;
        _clock = clock;
        _audit = audit;
        _grants = grants;
        _maintenance = maintenance;
        _caller = caller;
        _evaluator = evaluator;
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
                    .Select(grant => (Group: groupsById[grant.GroupId], grant.Roles))
                    .OrderBy(row => row.Group.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(row => new AccessTreeGroupDto(
                        row.Group.Id,
                        row.Group.Name,
                        row.Group.Members
                            .Select(member => resolve(member.UserId))
                            .OrderBy(user => user.DisplayName, StringComparer.OrdinalIgnoreCase)
                            .ToList(),
                        KnownRoles(application, row.Roles)))
                    .ToList(),
                userGrants
                    .Where(grant => grant.ApplicationId == application.Id)
                    .Select(grant => (User: resolve(grant.UserId), grant.Roles))
                    .OrderBy(row => row.User.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .Select(row => new AccessTreeUserDto(
                        row.User.Id,
                        row.User.Email,
                        row.User.DisplayName,
                        row.User.IsKnown,
                        KnownRoles(application, row.Roles)))
                    .ToList(),
                application.RolesToContract()))
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

        return Result<UserAccessDto>.Success(await ReadUserAccessAsync(userId, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<UserAccessDto>> GetMyAccessAsync(CancellationToken cancellationToken) =>
        _caller.UserId is Guid userId
            ? Result<UserAccessDto>.Success(await ReadUserAccessAsync(userId, cancellationToken).ConfigureAwait(false))
            : Result<UserAccessDto>.Failure(AccessErrors.SignInRequired);

    private async Task<UserAccessDto> ReadUserAccessAsync(Guid userId, CancellationToken cancellationToken)
    {
        var paths = await _graph.PathsOfUserAsync(userId, cancellationToken).ConfigureAwait(false);
        var effective = paths.Keys.ToArray();
        var memberships = await _db.Groups
            .AsNoTracking()
            .Where(group => effective.Contains(group.Id))
            .Select(group => new GroupReferenceDto(group.Id, group.Name))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var groupIds = memberships.Select(group => group.Id).ToArray();

        var direct = await _db.UserGrants
            .AsNoTracking()
            .Where(grant => grant.UserId == userId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var viaGroups = await _db.GroupGrants
            .AsNoTracking()
            .Where(grant => groupIds.Contains(grant.GroupId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var applicationIds = direct.Select(grant => grant.ApplicationId)
            .Concat(viaGroups.Select(grant => grant.ApplicationId))
            .Distinct()
            .ToArray();

        var applications = await _db.Applications
            .AsNoTracking()
            .Where(application => applicationIds.Contains(application.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var groupNames = memberships.ToDictionary(group => group.Id);

        var now = _clock.UtcNow;
        var access = new List<UserApplicationAccessDto>();
        var directByApplication = direct.ToDictionary(grant => grant.ApplicationId);
        var groupsByApplication = viaGroups.ToLookup(grant => grant.ApplicationId);
        foreach (var application in applications.OrderBy(application => application.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            var own = directByApplication.GetValueOrDefault(application.Id);
            var shared = groupsByApplication[application.Id].OrderBy(grant => groupNames[grant.GroupId].Name, StringComparer.OrdinalIgnoreCase).ToList();
            var sources = new List<UserAccessSourceDto>();
            var directActive = own?.IsActiveAt(now) == true;
            if (own is not null)
            {
                sources.Add(new UserAccessSourceDto([], own.ExpiresAtUtc, directActive));
            }

            var activeGroups = new List<ApplicationGroupGrant>();
            foreach (var grant in shared)
            {
                var path = new List<GroupReferenceDto>();
                for (Guid? groupId = grant.GroupId; groupId is Guid id; groupId = paths[id].Previous)
                {
                    path.Add(groupNames[id]);
                }

                path.Reverse();
                var membershipEnd = paths[grant.GroupId].ExpiresAtUtc;
                var end = grant.ExpiresAtUtc is null || membershipEnd < grant.ExpiresAtUtc ? membershipEnd : grant.ExpiresAtUtc;
                var active = end is null || end > now;
                sources.Add(new UserAccessSourceDto(path, end, active));
                if (active)
                {
                    activeGroups.Add(grant);
                }
            }

            var roles = (directActive ? own!.Roles : []).Concat(activeGroups.SelectMany(grant => grant.Roles));
            var outcome = application.Status != ApplicationStatus.Active ? "inactive"
                : directActive || activeGroups.Count > 0 ? "granted" : "expired";
            IReadOnlyList<string> organizations = [];
            var requireMultiFactor = false;
            if (outcome == "granted" && application.Organizations != OrganizationMode.None)
            {
                var organization = await _evaluator.EvaluateOrganizationAsync(
                    userId, application.ClientId, requested: null, interactive: true, cancellationToken).ConfigureAwait(false);
                outcome = organization.Outcome switch
                {
                    OrganizationOutcome.Missing => "organization_required",
                    OrganizationOutcome.ChoiceNeeded => "organization_choice",
                    _ => outcome,
                };
                organizations = organization.Organization is { } selected
                    ? [selected.Name]
                    : organization.Choices.Select(choice => choice.Name).ToList();
                roles = roles.Concat(organization.Roles ?? []);
                requireMultiFactor = organization.RequireMultiFactor;
            }

            var effectiveRoles = KnownRoles(application, roles);
            access.Add(new UserApplicationAccessDto(
                application.Id, application.ClientId, application.DisplayName, application.Status.ToContract(), own is not null,
                shared.Select(grant => groupNames[grant.GroupId]).ToList(),
                effectiveRoles, sources, outcome, organizations, requireMultiFactor,
                application.RolesToContract().Where(role => effectiveRoles.Contains(role.Key, StringComparer.Ordinal)).ToList()));
        }

        return new UserAccessDto(
            userId,
            memberships.OrderBy(group => group.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            access);
    }

    public async Task<Result> GrantUserAsync(
        Guid applicationId,
        Guid userId,
        IReadOnlyList<string>? roles,
        CancellationToken cancellationToken,
        ExpiryChange? expiry = null)
    {
        if (expiry?.ExpiresAtUtc is { } end && end <= _clock.UtcNow)
        {
            return Result.Failure(AccessErrors.ExpiryInPast);
        }

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

        var granted = await _grants.GrantAsync(application, userId, roles, notifyUser: true, cancellationToken, expiry).ConfigureAwait(false);

        return granted.IsSuccess ? Result.Success() : Result.Failure(granted.Error);
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

        await _grants.RevokeAsync(application, userId, cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    public async Task<Result> GrantGroupAsync(
        Guid applicationId,
        Guid groupId,
        IReadOnlyList<string>? roles,
        CancellationToken cancellationToken,
        ExpiryChange? expiry = null)
    {
        if (expiry?.ExpiresAtUtc is { } end && end <= _clock.UtcNow)
        {
            return Result.Failure(AccessErrors.ExpiryInPast);
        }

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

        roles = UserGrantOperations.NormaliseRoles(roles);
        var checkedRoles = CheckRoles(application, roles);
        if (checkedRoles.IsFailure)
        {
            return checkedRoles;
        }

        var group = await _db.Groups.AsNoTracking().SingleOrDefaultAsync(candidate => candidate.Id == groupId, cancellationToken).ConfigureAwait(false);
        if (group is null)
        {
            return Result.Failure(AccessErrors.GroupNotFound);
        }

        var grant = await _db.GroupGrants
            .SingleOrDefaultAsync(candidate => candidate.ApplicationId == applicationId && candidate.GroupId == groupId, cancellationToken)
            .ConfigureAwait(false);

        var created = grant is null;
        if (grant is null)
        {
            grant = new ApplicationGroupGrant(applicationId, groupId, _clock.UtcNow);
            _db.GroupGrants.Add(grant);
        }

        var rolesChanged = roles is not null && grant.SetRoles(roles);
        var expiryChanged = expiry is not null && grant.SetExpiry(expiry.ExpiresAtUtc);

        if (!created && !rolesChanged)
        {
            if (expiryChanged)
            {
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            return Result.Success();
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _audit.RecordAsync(
                AuditEvent.Succeeded(
                    created ? AccessAuditActions.GroupGranted : AccessAuditActions.GroupRolesChanged,
                    group.ToAuditSubject(),
                    AccessAudit.Details(
                        ("application", application.DisplayName),
                        ("clientId", application.ClientId),
                        ("roles", AccessAudit.JoinRoles(grant.Roles)))),
                cancellationToken)
            .ConfigureAwait(false);

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
            // The members of its subgroups held the application through it too.
            var members = await _graph.MembersOfGroupAsync(groupId, cancellationToken).ConfigureAwait(false);

            var groupName = await _db.Groups
                .Where(group => group.Id == groupId)
                .Select(group => group.Name)
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            _db.GroupGrants.Remove(grant);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await _access.RevokeIfNoLongerAllowedAsync(application, members, cancellationToken).ConfigureAwait(false);

            await _audit.RecordAsync(
                    AuditEvent.Succeeded(
                        AccessAuditActions.GroupRevoked,
                        new AuditSubject(AuditSubjectTypes.Group, groupId.ToString(), groupName),
                        AccessAudit.Details(("application", application.DisplayName), ("clientId", application.ClientId))),
                    cancellationToken)
                .ConfigureAwait(false);
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

        await _maintenance.ForgetUserAsync(userId, cancellationToken).ConfigureAwait(false);

        // Not audited on its own: it is the second half of deleting the account, which is.
        return Result.Success();
    }

    /// <summary>Every role key given must be one the application defines.</summary>
    private static Result CheckRoles(PortalApplication application, IReadOnlyList<string>? roles) =>
        roles is null || roles.All(application.HasRole)
            ? Result.Success()
            : Result.Failure(AccessErrors.RoleNotFound);

    /// <summary>The role keys among <paramref name="roles"/> that the application still defines, sorted.</summary>
    private static IReadOnlyList<string> KnownRoles(PortalApplication application, IEnumerable<string> roles) =>
        roles.Where(application.HasRole).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

    private Task<PortalApplication?> FindApplicationAsync(Guid applicationId, CancellationToken cancellationToken) =>
        _db.Applications.AsNoTracking().SingleOrDefaultAsync(application => application.Id == applicationId, cancellationToken);
}
