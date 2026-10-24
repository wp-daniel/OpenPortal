using Microsoft.EntityFrameworkCore;
using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Access.Application.Contracts;
using OpenPortal.Access.Domain;
using OpenPortal.Access.Domain.Applications;
using OpenPortal.Access.Infrastructure.Persistence;
using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Access.Infrastructure.Services;

/// <summary>
/// The application API: an application's backend looking at and changing who can open <em>it</em>. The
/// application is always the caller's own, and every call re-reads it, so disabling the application or
/// switching a scope off takes effect on the next request, not when the token expires.
/// </summary>
internal sealed class ApplicationApiService : IApplicationApiService
{
    private readonly AccessDbContext _db;
    private readonly AccessQueries _access;
    private readonly IUserDirectory _directory;
    private readonly IApplicationApiCaller _caller;
    private readonly UserGrantOperations _grants;
    private readonly GroupGraph _graph;
    private readonly OpenPortal.SharedKernel.Time.IClock _clock;

    public ApplicationApiService(
        AccessDbContext db,
        AccessQueries access,
        IUserDirectory directory,
        IApplicationApiCaller caller,
        UserGrantOperations grants,
        GroupGraph graph,
        OpenPortal.SharedKernel.Time.IClock clock)
    {
        _graph = graph;
        _clock = clock;
        _db = db;
        _access = access;
        _directory = directory;
        _caller = caller;
        _grants = grants;
    }

    public async Task<Result<ApplicationSelfDto>> GetSelfAsync(CancellationToken cancellationToken)
    {
        var application = await CallerAsync(scope: null, cancellationToken).ConfigureAwait(false);

        return application.IsSuccess
            ? Result<ApplicationSelfDto>.Success(application.Value.ToSelfDto())
            : Result<ApplicationSelfDto>.Failure(application.Error);
    }

    public async Task<Result<ApplicationUserPageDto>> ListUsersAsync(
        ApplicationUserListQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var application = await CallerAsync(ApplicationApiScopes.UsersRead, cancellationToken).ConfigureAwait(false);
        if (application.IsFailure)
        {
            return Result<ApplicationUserPageDto>.Failure(application.Error);
        }

        var access = await LoadAccessAsync(application.Value, userId: null, cancellationToken).ConfigureAwait(false);
        var users = await _directory.FindAsync(access.Keys, cancellationToken).ConfigureAwait(false);

        // Filtered and paged in memory: the users of one application are a modest set, and their names live in
        // the identity store, not here.
        var search = query.Search?.Trim().ToUpperInvariant();
        var matching = users.Values
            .Where(user => string.IsNullOrEmpty(search)
                || user.Email.ToUpperInvariant().Contains(search, StringComparison.Ordinal)
                || user.DisplayName.ToUpperInvariant().Contains(search, StringComparison.Ordinal))
            .OrderBy(user => user.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(user => user.Email, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, ApplicationUserListQuery.MaxPageSize);
        var items = matching.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        var groups = await GroupNamesAsync(application.Value, access, items.Select(user => user.Id).ToList(), cancellationToken).ConfigureAwait(false);

        return Result<ApplicationUserPageDto>.Success(new ApplicationUserPageDto(
            items.Select(user => ToDto(application.Value, user, access[user.Id], groups)).ToList(),
            page,
            pageSize,
            matching.Count));
    }

    public async Task<Result<ApplicationUserDto>> GetUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var application = await CallerAsync(ApplicationApiScopes.UsersRead, cancellationToken).ConfigureAwait(false);

        return application.IsSuccess
            ? await FindUserAsync(application.Value, userId, cancellationToken).ConfigureAwait(false)
            : Result<ApplicationUserDto>.Failure(application.Error);
    }

    public async Task<Result<ApplicationGrantResultDto>> GrantUserAsync(
        Guid userId,
        IReadOnlyList<string>? roles,
        CancellationToken cancellationToken)
    {
        var application = await CallerAsync(ApplicationApiScopes.AccessManage, cancellationToken).ConfigureAwait(false);
        if (application.IsFailure)
        {
            return Result<ApplicationGrantResultDto>.Failure(application.Error);
        }

        var granted = await _grants.GrantAsync(application.Value, userId, roles, cancellationToken).ConfigureAwait(false);
        if (granted.IsFailure)
        {
            return Result<ApplicationGrantResultDto>.Failure(granted.Error);
        }

        var user = await FindUserAsync(application.Value, userId, cancellationToken).ConfigureAwait(false);

        return user.IsSuccess
            ? Result<ApplicationGrantResultDto>.Success(new ApplicationGrantResultDto(user.Value, granted.Value))
            : Result<ApplicationGrantResultDto>.Failure(user.Error);
    }

    public async Task<Result<ApplicationRevokeResultDto>> RevokeUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var application = await CallerAsync(ApplicationApiScopes.AccessManage, cancellationToken).ConfigureAwait(false);
        if (application.IsFailure)
        {
            return Result<ApplicationRevokeResultDto>.Failure(application.Error);
        }

        var changed = await _grants.RevokeAsync(application.Value, userId, cancellationToken).ConfigureAwait(false);
        var stillHasAccess = await _access.HasGrantAsync(application.Value.Id, userId, cancellationToken).ConfigureAwait(false);

        return Result<ApplicationRevokeResultDto>.Success(new ApplicationRevokeResultDto(changed, stillHasAccess));
    }

    /// <summary>
    /// The calling application, provided it is active and, when <paramref name="scope"/> is given, both its token
    /// carries the scope and the scope is still enabled for it.
    /// </summary>
    private async Task<Result<PortalApplication>> CallerAsync(string? scope, CancellationToken cancellationToken)
    {
        if (_caller.ClientId is not { } clientId)
        {
            return Result<PortalApplication>.Failure(AccessErrors.ApiCallerUnknown);
        }

        var application = await _db.Applications
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.ClientId == clientId, cancellationToken)
            .ConfigureAwait(false);

        if (application is null || application.Status != ApplicationStatus.Active || application.ApiScopes.Count == 0)
        {
            return Result<PortalApplication>.Failure(AccessErrors.ApiCallerUnknown);
        }

        if (scope is not null && (!_caller.HasScope(scope) || !application.HasApiScope(scope)))
        {
            return Result<PortalApplication>.Failure(AccessErrors.ApiScopeMissing);
        }

        return Result<PortalApplication>.Success(application);
    }

    private async Task<Result<ApplicationUserDto>> FindUserAsync(
        PortalApplication application,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var access = await LoadAccessAsync(application, userId, cancellationToken).ConfigureAwait(false);
        if (!access.TryGetValue(userId, out var row))
        {
            return Result<ApplicationUserDto>.Failure(AccessErrors.UserHasNoAccess);
        }

        var users = await _directory.FindAsync([userId], cancellationToken).ConfigureAwait(false);
        if (!users.TryGetValue(userId, out var user))
        {
            return Result<ApplicationUserDto>.Failure(AccessErrors.UserHasNoAccess);
        }

        var groups = await GroupNamesAsync(application, access, [userId], cancellationToken).ConfigureAwait(false);

        return Result<ApplicationUserDto>.Success(ToDto(application, user, row, groups));
    }

    /// <summary>
    /// Who holds a grant on the application (all users, or only <paramref name="userId"/>): whether personally,
    /// the roles of every grant together, and the granted groups they belong to.
    /// </summary>
    private async Task<Dictionary<Guid, UserAccess>> LoadAccessAsync(
        PortalApplication application,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        var direct = (await _db.UserGrants
                .AsNoTracking()
                .Where(grant => grant.ApplicationId == application.Id && (userId == null || grant.UserId == userId))
                .Select(grant => new { grant.UserId, grant.Roles, grant.ExpiresAtUtc })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .Where(grant => grant.ExpiresAtUtc is null || grant.ExpiresAtUtc > now)
            .ToList();

        var groupGrants = (await _db.GroupGrants
                .AsNoTracking()
                .Where(grant => grant.ApplicationId == application.Id)
                .Select(grant => new { grant.GroupId, grant.Roles, grant.ExpiresAtUtc })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .Where(grant => grant.ExpiresAtUtc is null || grant.ExpiresAtUtc > now)
            .ToList();

        // Members of a granted group include the members of its subgroups.
        var viaGroups = new List<(Guid UserId, Guid GroupId, IReadOnlyList<string> Roles)>();
        foreach (var grant in groupGrants)
        {
            foreach (var member in await _graph.MembersOfGroupAsync(grant.GroupId, cancellationToken).ConfigureAwait(false))
            {
                if (userId == null || member == userId)
                {
                    viaGroups.Add((member, grant.GroupId, grant.Roles));
                }
            }
        }

        var access = new Dictionary<Guid, UserAccess>();

        UserAccess For(Guid id) => access.TryGetValue(id, out var row) ? row : access[id] = new UserAccess();

        foreach (var grant in direct)
        {
            var row = For(grant.UserId);
            row.Direct = true;
            row.Roles.UnionWith(grant.Roles);
        }

        foreach (var grant in viaGroups)
        {
            var row = For(grant.UserId);
            row.GrantedGroups.Add(grant.GroupId);
            row.Roles.UnionWith(grant.Roles);
        }

        return access;
    }

    /// <summary>The group names each of <paramref name="userIds"/> carries, as the application's setting allows.</summary>
    private async Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GroupNamesAsync(
        PortalApplication application,
        IReadOnlyDictionary<Guid, UserAccess> access,
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken)
    {
        if (application.GroupClaims == GroupClaimMode.None || userIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<string>>();
        }

        var groupsOf = new Dictionary<Guid, HashSet<Guid>>();
        foreach (var id in userIds)
        {
            groupsOf[id] = await _graph.GroupsOfUserAsync(id, cancellationToken).ConfigureAwait(false);
        }

        var allGroups = groupsOf.Values.SelectMany(groups => groups).ToHashSet();
        var names = await _db.Groups
            .AsNoTracking()
            .Where(group => allGroups.Contains(group.Id))
            .ToDictionaryAsync(group => group.Id, group => group.Name, cancellationToken)
            .ConfigureAwait(false);

        var memberships = groupsOf
            .SelectMany(entry => entry.Value.Where(names.ContainsKey).Select(group => new { UserId = entry.Key, Id = group, Name = names[group] }))
            .ToList();

        return memberships
            .Where(row => application.GroupClaims == GroupClaimMode.All
                || (access.TryGetValue(row.UserId, out var granted) && granted.GrantedGroups.Contains(row.Id)))
            .GroupBy(row => row.UserId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group.Select(row => row.Name).Order(StringComparer.Ordinal).ToList());
    }

    private static ApplicationUserDto ToDto(
        PortalApplication application,
        DirectoryUser user,
        UserAccess access,
        IReadOnlyDictionary<Guid, IReadOnlyList<string>> groups) => new(
        user.Id,
        user.Email,
        user.DisplayName,
        access.Direct,
        access.Roles.Where(application.HasRole).Order(StringComparer.Ordinal).ToList(),
        groups.TryGetValue(user.Id, out var names) ? names : []);

    private sealed class UserAccess
    {
        public bool Direct { get; set; }

        public HashSet<string> Roles { get; } = new(StringComparer.Ordinal);

        public HashSet<Guid> GrantedGroups { get; } = [];
    }
}
