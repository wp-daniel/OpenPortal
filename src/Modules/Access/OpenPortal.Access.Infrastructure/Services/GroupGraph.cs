using Microsoft.EntityFrameworkCore;
using OpenPortal.Access.Infrastructure.Persistence;
using OpenPortal.SharedKernel.Time;

namespace OpenPortal.Access.Infrastructure.Services;

/// <summary>
/// Effective group membership: a user belongs to the groups they were added to (while the membership has not
/// expired) and to every group those are nested in, transitively. Nestings are few, so the whole graph is read
/// once per request and walked in memory.
/// </summary>
internal sealed class GroupGraph
{
    private readonly AccessDbContext _db;
    private readonly IClock _clock;
    private ILookup<Guid, Guid>? _parentsOf;
    private ILookup<Guid, Guid>? _childrenOf;

    public GroupGraph(AccessDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    /// <summary>Every group the user belongs to, directly or through a subgroup.</summary>
    public async Task<HashSet<Guid>> GroupsOfUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var memberships = await ActiveMembershipsAsync(userId, cancellationToken).ConfigureAwait(false);
        return await AncestorsAndSelfAsync(memberships.Select(member => member.GroupId), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>One path per effective group, preferring the membership that keeps the path open longest.</summary>
    public async Task<IReadOnlyDictionary<Guid, MembershipPath>> PathsOfUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var memberships = await ActiveMembershipsAsync(userId, cancellationToken).ConfigureAwait(false);
        var (parents, _) = await GraphAsync(cancellationToken).ConfigureAwait(false);
        var paths = new Dictionary<Guid, MembershipPath>();
        var queue = new Queue<(Guid GroupId, Guid? Previous)>();

        foreach (var member in memberships.OrderByDescending(member => member.ExpiresAtUtc ?? DateTimeOffset.MaxValue).ThenBy(member => member.GroupId))
        {
            queue.Enqueue((member.GroupId, null));
            while (queue.TryDequeue(out var step))
            {
                if (!paths.TryAdd(step.GroupId, new MembershipPath(step.Previous, member.ExpiresAtUtc)))
                {
                    continue;
                }

                foreach (var parent in parents[step.GroupId].Order())
                {
                    queue.Enqueue((parent, step.GroupId));
                }
            }
        }

        return paths;
    }

    private async Task<IReadOnlyList<Membership>> ActiveMembershipsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        return await _db.GroupMembers.AsNoTracking()
            .Where(member => member.UserId == userId && (member.ExpiresAtUtc == null || member.ExpiresAtUtc > now))
            .Select(member => new Membership(member.GroupId, member.ExpiresAtUtc))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    private sealed record Membership(Guid GroupId, DateTimeOffset? ExpiresAtUtc);

    internal sealed record MembershipPath(Guid? Previous, DateTimeOffset? ExpiresAtUtc);

    /// <summary>Every user who belongs to the group, directly or through one of its subgroups.</summary>
    public async Task<HashSet<Guid>> MembersOfGroupAsync(Guid groupId, CancellationToken cancellationToken)
    {
        var groups = await DescendantsAndSelfAsync([groupId], cancellationToken).ConfigureAwait(false);
        var now = _clock.UtcNow;

        var members = await _db.GroupMembers
            .AsNoTracking()
            .Where(member => groups.Contains(member.GroupId))
            .Select(member => new { member.UserId, member.ExpiresAtUtc })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return members.Where(member => member.ExpiresAtUtc is null || member.ExpiresAtUtc > now).Select(member => member.UserId).ToHashSet();
    }

    /// <summary>The groups and every group they are nested in.</summary>
    public async Task<HashSet<Guid>> AncestorsAndSelfAsync(IEnumerable<Guid> groupIds, CancellationToken cancellationToken)
    {
        var (parents, _) = await GraphAsync(cancellationToken).ConfigureAwait(false);

        return Walk(groupIds, parents);
    }

    /// <summary>The groups and every subgroup inside them.</summary>
    public async Task<HashSet<Guid>> DescendantsAndSelfAsync(IEnumerable<Guid> groupIds, CancellationToken cancellationToken)
    {
        var (_, children) = await GraphAsync(cancellationToken).ConfigureAwait(false);

        return Walk(groupIds, children);
    }

    /// <summary>Whether putting <paramref name="childId"/> inside <paramref name="parentId"/> would make a cycle.</summary>
    public async Task<bool> WouldCycleAsync(Guid parentId, Guid childId, CancellationToken cancellationToken) =>
        parentId == childId
        || (await DescendantsAndSelfAsync([childId], cancellationToken).ConfigureAwait(false)).Contains(parentId);

    /// <summary>Forgets the graph read so far, after a nesting changed in this request.</summary>
    public void Invalidate()
    {
        _parentsOf = null;
        _childrenOf = null;
    }

    private async Task<(ILookup<Guid, Guid> Parents, ILookup<Guid, Guid> Children)> GraphAsync(CancellationToken cancellationToken)
    {
        if (_parentsOf is null || _childrenOf is null)
        {
            var edges = await _db.GroupNestings
                .AsNoTracking()
                .Select(nesting => new { nesting.ParentGroupId, nesting.ChildGroupId })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            _parentsOf = edges.ToLookup(edge => edge.ChildGroupId, edge => edge.ParentGroupId);
            _childrenOf = edges.ToLookup(edge => edge.ParentGroupId, edge => edge.ChildGroupId);
        }

        return (_parentsOf, _childrenOf);
    }

    private static HashSet<Guid> Walk(IEnumerable<Guid> start, ILookup<Guid, Guid> next)
    {
        var seen = new HashSet<Guid>();
        var queue = new Queue<Guid>(start);

        while (queue.TryDequeue(out var group))
        {
            if (!seen.Add(group))
            {
                continue;
            }

            foreach (var neighbour in next[group])
            {
                queue.Enqueue(neighbour);
            }
        }

        return seen;
    }
}
