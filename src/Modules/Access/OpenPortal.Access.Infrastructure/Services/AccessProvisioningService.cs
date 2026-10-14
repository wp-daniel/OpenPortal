using Microsoft.EntityFrameworkCore;
using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Access.Application.Contracts;
using OpenPortal.Access.Domain;
using OpenPortal.Access.Domain.Applications;
using OpenPortal.Access.Domain.Organizations;
using OpenPortal.Access.Domain.Requests;
using OpenPortal.Access.Infrastructure.Persistence;
using OpenPortal.SharedKernel.Auditing;
using OpenPortal.SharedKernel.Notifications;
using OpenPortal.SharedKernel.Results;
using OpenPortal.SharedKernel.Time;
using OpenPortal.Access.Application.Auditing;
using OpenPortal.Access.Application.Notifications;

namespace OpenPortal.Access.Infrastructure.Services;

/// <summary>
/// Memberships an invitation promised, applied once the invited person has an account. The caller checked who
/// may promise them when the invitation was made (<see cref="ValidateMembershipsAsync"/>).
/// </summary>
internal sealed class AccessProvisioningService : IAccessProvisioningService
{
    private readonly AccessDbContext _db;
    private readonly IAccessAdminAuthorization _authorization;
    private readonly IClock _clock;
    private readonly IAuditTrail _audit;
    private readonly INotifier _notifier;

    public AccessProvisioningService(
        AccessDbContext db,
        IAccessAdminAuthorization authorization,
        IClock clock,
        IAuditTrail audit,
        INotifier notifier)
    {
        _db = db;
        _authorization = authorization;
        _clock = clock;
        _audit = audit;
        _notifier = notifier;
    }

    public async Task<Result> ValidateMembershipsAsync(
        IReadOnlyCollection<Guid> groupIds,
        IReadOnlyCollection<Guid> organizationIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(groupIds);
        ArgumentNullException.ThrowIfNull(organizationIds);

        if (groupIds.Count > 0)
        {
            var guard = await _authorization.EnsureCanAsync(AccessOperation.ManageGroups, cancellationToken).ConfigureAwait(false);
            if (guard.IsFailure)
            {
                return guard;
            }

            var ids = groupIds.Distinct().ToArray();
            var found = await _db.Groups.CountAsync(group => ids.Contains(group.Id), cancellationToken).ConfigureAwait(false);
            if (found != ids.Length)
            {
                return Result.Failure(AccessErrors.GroupNotFound);
            }
        }

        if (organizationIds.Count > 0)
        {
            var guard = await _authorization.EnsureCanAsync(AccessOperation.ManageOrganizations, cancellationToken).ConfigureAwait(false);
            if (guard.IsFailure)
            {
                return guard;
            }

            var ids = organizationIds.Distinct().ToArray();
            var found = await _db.Organizations.CountAsync(organization => ids.Contains(organization.Id), cancellationToken).ConfigureAwait(false);
            if (found != ids.Length)
            {
                return Result.Failure(AccessErrors.OrganizationNotFound);
            }
        }

        return Result.Success();
    }

    public async Task ApplyMembershipsAsync(
        Guid userId,
        string? userEmail,
        IReadOnlyCollection<Guid> groupIds,
        IReadOnlyCollection<Guid> organizationIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(groupIds);
        ArgumentNullException.ThrowIfNull(organizationIds);

        var now = _clock.UtcNow;
        var groupIdArray = groupIds.Distinct().ToArray();
        var organizationIdArray = organizationIds.Distinct().ToArray();

        // Groups or organizations deleted since the invitation was made are skipped.
        var groups = await _db.Groups
            .Include(group => group.Members)
            .Where(group => groupIdArray.Contains(group.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var organizations = await _db.Organizations
            .Include(organization => organization.Members)
            .Where(organization => organizationIdArray.Contains(organization.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var joinedGroups = groups.Where(group => group.AddMember(userId, now)).ToList();
        var joinedOrganizations = organizations
            .Where(organization => organization.SetMember(userId, OrganizationRole.Member, now) != MembershipChange.None)
            .ToList();

        if (joinedGroups.Count == 0 && joinedOrganizations.Count == 0)
        {
            return;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var user = new AuditSubject(AuditSubjectTypes.User, userId.ToString(), userEmail);

        foreach (var group in joinedGroups)
        {
            await _audit.RecordAsync(
                    AuditEvent.Succeeded(
                        AccessAuditActions.GroupMemberAdded,
                        user,
                        AccessAudit.Details(("group", group.Name), ("groupId", group.Id.ToString()), ("via", "invitation"))),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (var organization in joinedOrganizations)
        {
            await _audit.RecordAsync(
                    AuditEvent.Succeeded(
                        AccessAuditActions.OrganizationMemberAdded,
                        user,
                        AccessAudit.Details(
                            ("organization", organization.Name),
                            ("organizationId", organization.Id.ToString()),
                            ("role", OrganizationRoles.Member),
                            ("via", "invitation"))),
                    cancellationToken)
                .ConfigureAwait(false);

            await _notifier.NotifyAsync(
                    new Notice
                    {
                        Type = AccessNotificationTypes.OrganizationJoined,
                        Audience = NoticeAudience.Users(userId),
                        Parameters = new Dictionary<string, string?>
                        {
                            ["organization"] = organization.Name,
                            ["role"] = OrganizationRoles.Member,
                        },
                        Link = "/account?tab=organizations",

                        // The invited person is the caller here, but it was the inviter who chose the organization.
                        IncludeCaller = true,
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
