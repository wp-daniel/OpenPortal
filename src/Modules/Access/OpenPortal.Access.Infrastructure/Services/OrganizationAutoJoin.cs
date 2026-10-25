using Microsoft.EntityFrameworkCore;
using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Access.Application.Auditing;
using OpenPortal.Access.Application.Contracts;
using OpenPortal.Access.Application.Notifications;
using OpenPortal.Access.Domain.Organizations;
using OpenPortal.Access.Infrastructure.Persistence;
using OpenPortal.SharedKernel.Auditing;
using OpenPortal.SharedKernel.Notifications;
using OpenPortal.SharedKernel.Time;

namespace OpenPortal.Access.Infrastructure.Services;

/// <inheritdoc />
internal sealed class OrganizationAutoJoin : IOrganizationAutoJoin
{
    private readonly AccessDbContext _db;
    private readonly IUserDirectory _directory;
    private readonly IClock _clock;
    private readonly IAuditTrail _audit;
    private readonly INotifier _notifier;

    public OrganizationAutoJoin(AccessDbContext db, IUserDirectory directory, IClock clock, IAuditTrail audit, INotifier notifier)
    {
        _db = db;
        _directory = directory;
        _clock = clock;
        _audit = audit;
        _notifier = notifier;
    }

    public async Task<IReadOnlyList<OrganizationMembershipDto>> JoinByEmailDomainAsync(Guid userId, CancellationToken cancellationToken)
    {
        var users = await _directory.FindAsync([userId], cancellationToken).ConfigureAwait(false);

        // Only an address the account proved it controls says which organization it belongs to.
        if (!users.TryGetValue(userId, out var user) || !user.EmailConfirmed)
        {
            return [];
        }

        var at = user.Email.LastIndexOf('@');
        if (at <= 0)
        {
            return [];
        }

        var domainName = user.Email[(at + 1)..].Trim().ToLowerInvariant();
        var domains = await _db.OrganizationDomains
            .AsNoTracking()
            .Where(domain => domain.Domain == domainName && domain.AutoJoin && domain.VerifiedAtUtc != null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (domains.Count == 0)
        {
            return [];
        }

        var organizationIds = domains.Select(domain => domain.OrganizationId).ToList();
        var organizations = await _db.Organizations
            .Include(organization => organization.Members)
            .Where(organization => organizationIds.Contains(organization.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var joined = new List<OrganizationMembershipDto>();
        foreach (var organization in organizations)
        {
            if (organization.Members.Any(member => member.UserId == userId))
            {
                continue;
            }

            var role = domains.First(domain => domain.OrganizationId == organization.Id).AutoJoinRole;
            organization.SetMember(userId, role, _clock.UtcNow);
            joined.Add(new OrganizationMembershipDto(organization.Id, organization.Slug, organization.Name, OrganizationService.ToContract(role)));
        }

        if (joined.Count == 0)
        {
            return [];
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // Two sign-ins of the same account joined at the same moment; the other one recorded it.
            return [];
        }

        foreach (var membership in joined)
        {
            await _audit.RecordAsync(
                    new AuditEvent
                    {
                        Action = AccessAuditActions.OrganizationJoinedByDomain,
                        Actor = new AuditSubject(AuditSubjectTypes.User, userId.ToString(), user.Email),
                        Target = new AuditSubject(AuditSubjectTypes.Organization, membership.Id.ToString(), membership.Name),
                        Details = AccessAudit.Details(("domain", domainName), ("role", membership.Role)),
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            await _notifier.NotifyAsync(
                    new Notice
                    {
                        Type = AccessNotificationTypes.OrganizationJoined,
                        Audience = NoticeAudience.Users(userId),
                        Parameters = new Dictionary<string, string?> { ["organization"] = membership.Name, ["role"] = membership.Role },
                        Link = "/account?tab=organizations",
                        IncludeCaller = true,
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return joined;
    }
}
