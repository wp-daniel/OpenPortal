using Microsoft.EntityFrameworkCore;
using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Access.Application.Auditing;
using OpenPortal.Access.Application.Contracts;
using OpenPortal.Access.Domain;
using OpenPortal.Access.Domain.Grants;
using OpenPortal.Access.Infrastructure.Persistence;
using OpenPortal.SharedKernel.Auditing;
using OpenPortal.SharedKernel.Results;
using OpenPortal.SharedKernel.Time;

namespace OpenPortal.Access.Infrastructure.Services;

/// <summary>Grants of portal pages to groups, and the matrix that shows them.</summary>
internal sealed class PagePermissionService : IPagePermissionService
{
    private readonly AccessDbContext _db;
    private readonly IPortalPageCatalog _catalog;
    private readonly IAccessAdminAuthorization _authorization;
    private readonly IClock _clock;
    private readonly IAuditTrail _audit;

    public PagePermissionService(
        AccessDbContext db,
        IPortalPageCatalog catalog,
        IAccessAdminAuthorization authorization,
        IClock clock,
        IAuditTrail audit)
    {
        _audit = audit;
        _db = db;
        _catalog = catalog;
        _authorization = authorization;
        _clock = clock;
    }

    public async Task<Result<PagePermissionMatrixDto>> GetMatrixAsync(CancellationToken cancellationToken)
    {
        var guard = await _authorization
            .EnsureCanAsync(AccessOperation.ManagePagePermissions, cancellationToken)
            .ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<PagePermissionMatrixDto>.Failure(guard.Error);
        }

        var groups = await _db.Groups
            .AsNoTracking()
            .Select(group => new GroupReferenceDto(group.Id, group.Name))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var grants = await _db.PageGrants
            .AsNoTracking()
            .Select(grant => new PageGrantDto(grant.PageKey, grant.GroupId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result<PagePermissionMatrixDto>.Success(new PagePermissionMatrixDto(
            _catalog.Pages,
            groups.OrderBy(group => group.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            grants.Where(grant => _catalog.IsKnown(grant.PageKey)).ToList()));
    }

    public async Task<Result> GrantAsync(string pageKey, Guid groupId, CancellationToken cancellationToken)
    {
        var target = await ValidateAsync(pageKey, groupId, cancellationToken).ConfigureAwait(false);
        if (target.IsFailure)
        {
            return target;
        }

        if (await FindAsync(pageKey, groupId, cancellationToken).ConfigureAwait(false) is not null)
        {
            return Result.Success();
        }

        var grant = PageGroupGrant.Create(pageKey, groupId, _clock.UtcNow);
        if (grant.IsFailure)
        {
            return Result.Failure(grant.Error);
        }

        _db.PageGrants.Add(grant.Value);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await RecordAsync(AccessAuditActions.PageGranted, groupId, AccessAudit.Details(("page", pageKey)), cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    public async Task<Result> RevokeAsync(string pageKey, Guid groupId, CancellationToken cancellationToken)
    {
        var target = await ValidateAsync(pageKey, groupId, cancellationToken).ConfigureAwait(false);
        if (target.IsFailure)
        {
            return target;
        }

        var grant = await FindAsync(pageKey, groupId, cancellationToken).ConfigureAwait(false);
        if (grant is not null)
        {
            _db.PageGrants.Remove(grant);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await RecordAsync(AccessAuditActions.PageRevoked, groupId, AccessAudit.Details(("page", pageKey)), cancellationToken).ConfigureAwait(false);
        }

        return Result.Success();
    }

    public async Task<Result> SetGroupPagesAsync(
        Guid groupId,
        IReadOnlyCollection<string> pageKeys,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pageKeys);

        var target = await ValidateAsync(pageKeys, groupId, cancellationToken).ConfigureAwait(false);
        if (target.IsFailure)
        {
            return target;
        }

        var wanted = pageKeys.ToHashSet(StringComparer.Ordinal);
        var current = await _db.PageGrants
            .Where(grant => grant.GroupId == groupId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Grants on keys the host no longer declares are left alone: they grant nothing and are not shown.
        var removed = current.Where(grant => _catalog.IsKnown(grant.PageKey) && !wanted.Contains(grant.PageKey)).ToList();
        _db.PageGrants.RemoveRange(removed);

        var added = wanted.Where(key => current.All(grant => grant.PageKey != key)).Order(StringComparer.Ordinal).ToList();

        var now = _clock.UtcNow;
        foreach (var key in added)
        {
            var grant = PageGroupGrant.Create(key, groupId, now);
            if (grant.IsFailure)
            {
                return Result.Failure(grant.Error);
            }

            _db.PageGrants.Add(grant.Value);
        }

        if (removed.Count == 0 && added.Count == 0)
        {
            return Result.Success();
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await RecordAsync(
                AccessAuditActions.GroupPagesChanged,
                groupId,
                AccessAudit.Details(
                    ("added", added.Count > 0 ? string.Join(", ", added) : null),
                    ("removed", removed.Count > 0 ? string.Join(", ", removed.Select(grant => grant.PageKey).Order(StringComparer.Ordinal)) : null)),
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success();
    }

    private async Task RecordAsync(string action, Guid groupId, Dictionary<string, string?> details, CancellationToken cancellationToken)
    {
        var name = await _db.Groups
            .Where(group => group.Id == groupId)
            .Select(group => group.Name)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        await _audit.RecordAsync(
                AuditEvent.Succeeded(action, new AuditSubject(AuditSubjectTypes.Group, groupId.ToString(), name), details),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private Task<Result> ValidateAsync(string pageKey, Guid groupId, CancellationToken cancellationToken) =>
        ValidateAsync([pageKey], groupId, cancellationToken);

    private async Task<Result> ValidateAsync(
        IEnumerable<string> pageKeys,
        Guid groupId,
        CancellationToken cancellationToken)
    {
        var guard = await _authorization
            .EnsureCanAsync(AccessOperation.ManagePagePermissions, cancellationToken)
            .ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return guard;
        }

        if (!pageKeys.All(_catalog.IsKnown))
        {
            return Result.Failure(AccessErrors.PageNotFound);
        }

        var groupExists = await _db.Groups.AnyAsync(group => group.Id == groupId, cancellationToken).ConfigureAwait(false);

        return groupExists ? Result.Success() : Result.Failure(AccessErrors.GroupNotFound);
    }

    private Task<PageGroupGrant?> FindAsync(string pageKey, Guid groupId, CancellationToken cancellationToken) =>
        _db.PageGrants.SingleOrDefaultAsync(
            grant => grant.PageKey == pageKey && grant.GroupId == groupId,
            cancellationToken);
}

/// <summary>The page rule, written once: a user may open a page when one of their groups holds it.</summary>
internal sealed class PagePermissionEvaluator : IPagePermissionEvaluator
{
    private readonly AccessDbContext _db;
    private readonly IPortalPageCatalog _catalog;

    public PagePermissionEvaluator(AccessDbContext db, IPortalPageCatalog catalog)
    {
        _db = db;
        _catalog = catalog;
    }

    public async Task<IReadOnlyList<string>> GetPagesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var keys = await _db.GroupMembers
            .AsNoTracking()
            .Where(member => member.UserId == userId)
            .Join(_db.PageGrants, member => member.GroupId, grant => grant.GroupId, (_, grant) => grant.PageKey)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Keys of pages the host no longer declares are left in the table but grant nothing.
        return keys.Where(_catalog.IsKnown).Order(StringComparer.Ordinal).ToList();
    }
}
