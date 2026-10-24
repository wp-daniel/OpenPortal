using Microsoft.EntityFrameworkCore;
using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Access.Application.Auditing;
using OpenPortal.Access.Application.Contracts;
using OpenPortal.Access.Application.Notifications;
using OpenPortal.Access.Domain;
using OpenPortal.Access.Domain.Applications;
using OpenPortal.Access.Domain.Organizations;
using OpenPortal.Access.Infrastructure.Persistence;
using OpenPortal.SharedKernel.Auditing;
using OpenPortal.SharedKernel.Notifications;
using OpenPortal.SharedKernel.Results;
using OpenPortal.SharedKernel.Time;

namespace OpenPortal.Access.Infrastructure.Services;

/// <summary>Organizations and their members.</summary>
internal sealed class OrganizationService : IOrganizationService
{
    private readonly AccessDbContext _db;
    private readonly OidcClientRegistry _clients;
    private readonly IUserDirectory _directory;
    private readonly IAccessAdminAuthorization _authorization;
    private readonly IAccessCaller _caller;
    private readonly IClock _clock;
    private readonly IAuditTrail _audit;
    private readonly INotifier _notifier;

    public OrganizationService(
        AccessDbContext db,
        OidcClientRegistry clients,
        IUserDirectory directory,
        IAccessAdminAuthorization authorization,
        IAccessCaller caller,
        IClock clock,
        IAuditTrail audit,
        INotifier notifier)
    {
        _db = db;
        _clients = clients;
        _directory = directory;
        _authorization = authorization;
        _caller = caller;
        _clock = clock;
        _audit = audit;
        _notifier = notifier;
    }

    public async Task<Result<IReadOnlyList<OrganizationSummaryDto>>> ListAsync(CancellationToken cancellationToken)
    {
        var guard = await _authorization.EnsureCanAsync(AccessOperation.ListOrganizations, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<IReadOnlyList<OrganizationSummaryDto>>.Failure(guard.Error);
        }

        var organizations = await _db.Organizations
            .AsNoTracking()
            .Select(organization => new
            {
                organization.Id,
                organization.Name,
                organization.Slug,
                organization.Description,
                MemberCount = organization.Members.Count,
                organization.CreatedAtUtc,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<OrganizationSummaryDto> result = organizations
            .OrderBy(organization => organization.Name, StringComparer.OrdinalIgnoreCase)
            .Select(organization => new OrganizationSummaryDto(
                organization.Id,
                organization.Name,
                organization.Slug,
                organization.Description,
                organization.MemberCount,
                organization.CreatedAtUtc))
            .ToList();

        return Result<IReadOnlyList<OrganizationSummaryDto>>.Success(result);
    }

    public async Task<Result<OrganizationDetailDto>> GetAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var organization = await FindAsync(organizationId, cancellationToken).ConfigureAwait(false);

        var guard = await EnsureCanManageMembersAsync(organization, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<OrganizationDetailDto>.Failure(guard.Error);
        }

        return Result<OrganizationDetailDto>.Success(await ToDetailAsync(organization!, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<OrganizationDetailDto>> AddMemberByEmailAsync(
        Guid organizationId,
        AddOrganizationMemberRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var organization = await FindAsync(organizationId, cancellationToken).ConfigureAwait(false);
        var guard = await EnsureCanManageMembersAsync(organization, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<OrganizationDetailDto>.Failure(guard.Error);
        }

        var user = string.IsNullOrWhiteSpace(request.Email)
            ? null
            : await _directory.FindByEmailAsync(request.Email.Trim(), cancellationToken).ConfigureAwait(false);

        return user is null
            ? Result<OrganizationDetailDto>.Failure(AccessErrors.UserNotFound)
            : await SetMemberAsync(organizationId, user.Id, new SetOrganizationMemberRequest(request.Role), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Members are managed by the portal's administrators (and holders of the organizations page) and by the
    /// organization's own administrators: delegated management, so a tenant looks after its own people.
    /// </summary>
    private async Task<Result> EnsureCanManageMembersAsync(Organization? organization, CancellationToken cancellationToken)
    {
        var portal = await _authorization.EnsureCanAsync(AccessOperation.ManageOrganizations, cancellationToken).ConfigureAwait(false);

        if (organization is null)
        {
            // Only someone who could manage organizations at all learns that it does not exist.
            return portal.IsSuccess ? Result.Failure(AccessErrors.OrganizationNotFound) : Result.Failure(AccessErrors.OrganizationManageForbidden);
        }

        return portal.IsSuccess || IsOwnAdministrator(organization)
            ? Result.Success()
            : Result.Failure(AccessErrors.OrganizationManageForbidden);
    }

    private bool IsOwnAdministrator(Organization organization) =>
        _caller.UserId is Guid me && organization.Members.Any(member => member.UserId == me && member.Role == OrganizationRole.Admin);

    /// <summary>A delegated administrator cannot leave the organization without one (portal administrators can).</summary>
    private async Task<bool> WouldLoseLastAdminAsync(Organization organization, Guid userId, OrganizationRole? newRole, CancellationToken cancellationToken)
    {
        if ((await _authorization.EnsureCanAsync(AccessOperation.ManageOrganizations, cancellationToken).ConfigureAwait(false)).IsSuccess)
        {
            return false;
        }

        var admins = organization.Members.Where(member => member.Role == OrganizationRole.Admin).Select(member => member.UserId).ToList();

        return admins.Count == 1 && admins[0] == userId && newRole != OrganizationRole.Admin;
    }

    public async Task<Result<OrganizationDetailDto>> CreateAsync(CreateOrganizationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var guard = await _authorization.EnsureCanAsync(AccessOperation.ManageOrganizations, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<OrganizationDetailDto>.Failure(guard.Error);
        }

        var created = Organization.Create(Guid.NewGuid(), request.Name, request.Slug, request.Description, _clock.UtcNow);
        if (created.IsFailure)
        {
            return Result<OrganizationDetailDto>.Failure(created.Error);
        }

        var organization = created.Value;

        if (await _db.Organizations.AnyAsync(candidate => candidate.Slug == organization.Slug, cancellationToken).ConfigureAwait(false))
        {
            return Result<OrganizationDetailDto>.Failure(AccessErrors.OrganizationSlugInUse);
        }

        _db.Organizations.Add(organization);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _audit.RecordAsync(AuditEvent.Succeeded(AccessAuditActions.OrganizationCreated, organization.ToAuditSubject()), cancellationToken)
            .ConfigureAwait(false);

        return Result<OrganizationDetailDto>.Success(await ToDetailAsync(organization, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<OrganizationDetailDto>> UpdateAsync(
        Guid organizationId,
        UpdateOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var guard = await _authorization.EnsureCanAsync(AccessOperation.ManageOrganizations, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<OrganizationDetailDto>.Failure(guard.Error);
        }

        var organization = await FindAsync(organizationId, cancellationToken).ConfigureAwait(false);
        if (organization is null)
        {
            return Result<OrganizationDetailDto>.Failure(AccessErrors.OrganizationNotFound);
        }

        var updated = organization.Update(request.Name, request.Description, _clock.UtcNow);
        if (updated.IsFailure)
        {
            return Result<OrganizationDetailDto>.Failure(updated.Error);
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // The new name reaches the applications at their next refresh: the token endpoint reads it every time.
        await _audit.RecordAsync(AuditEvent.Succeeded(AccessAuditActions.OrganizationUpdated, organization.ToAuditSubject()), cancellationToken)
            .ConfigureAwait(false);

        return Result<OrganizationDetailDto>.Success(await ToDetailAsync(organization, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result> DeleteAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var guard = await _authorization.EnsureCanAsync(AccessOperation.ManageOrganizations, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return guard;
        }

        var organization = await FindAsync(organizationId, cancellationToken).ConfigureAwait(false);
        if (organization is null)
        {
            return Result.Failure(AccessErrors.OrganizationNotFound);
        }

        var subject = organization.ToAuditSubject();
        var members = organization.Members.Select(member => member.UserId).ToList();

        _db.Organizations.Remove(organization);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await RevokeOrganizationSessionsAsync(members, cancellationToken).ConfigureAwait(false);

        await _audit.RecordAsync(
                AuditEvent.Succeeded(
                    AccessAuditActions.OrganizationDeleted,
                    subject,
                    AccessAudit.Details(("members", members.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)))),
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success();
    }

    public async Task<Result<OrganizationDetailDto>> SetMemberAsync(
        Guid organizationId,
        Guid userId,
        SetOrganizationMemberRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var organization = await FindAsync(organizationId, cancellationToken).ConfigureAwait(false);
        var guard = await EnsureCanManageMembersAsync(organization, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<OrganizationDetailDto>.Failure(guard.Error);
        }

        if (ParseRole(request.Role ?? OrganizationRoles.Member) is not { } role)
        {
            return Result<OrganizationDetailDto>.Failure(AccessErrors.OrganizationRoleInvalid);
        }

        if (await WouldLoseLastAdminAsync(organization!, userId, role, cancellationToken).ConfigureAwait(false))
        {
            return Result<OrganizationDetailDto>.Failure(AccessErrors.OrganizationLastAdmin);
        }

        var users = await _directory.FindAsync([userId], cancellationToken).ConfigureAwait(false);
        if (!users.TryGetValue(userId, out var user))
        {
            return Result<OrganizationDetailDto>.Failure(AccessErrors.UserNotFound);
        }

        var change = organization!.SetMember(userId, role, _clock.UtcNow);
        if (change != MembershipChange.None)
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await _audit.RecordAsync(
                    AuditEvent.Succeeded(
                        change == MembershipChange.Added
                            ? AccessAuditActions.OrganizationMemberAdded
                            : AccessAuditActions.OrganizationMemberRoleChanged,
                        new AuditSubject(AuditSubjectTypes.User, userId.ToString(), user.Email),
                        AccessAudit.Details(
                            ("organization", organization.Name),
                            ("organizationId", organization.Id.ToString()),
                            ("role", ToContract(role)))),
                    cancellationToken)
                .ConfigureAwait(false);

            if (change == MembershipChange.Added)
            {
                await _notifier.NotifyAsync(
                        new Notice
                        {
                            Type = AccessNotificationTypes.OrganizationJoined,
                            Audience = NoticeAudience.Users(userId),
                            Parameters = new Dictionary<string, string?>
                            {
                                ["organization"] = organization.Name,
                                ["role"] = ToContract(role),
                            },
                            Link = "/account?tab=organizations",
                        },
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return Result<OrganizationDetailDto>.Success(await ToDetailAsync(organization, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<OrganizationDetailDto>> RemoveMemberAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var organization = await FindAsync(organizationId, cancellationToken).ConfigureAwait(false);
        var guard = await EnsureCanManageMembersAsync(organization, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<OrganizationDetailDto>.Failure(guard.Error);
        }

        if (await WouldLoseLastAdminAsync(organization!, userId, newRole: null, cancellationToken).ConfigureAwait(false))
        {
            return Result<OrganizationDetailDto>.Failure(AccessErrors.OrganizationLastAdmin);
        }

        if (organization!.RemoveMember(userId))
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await RevokeOrganizationSessionsAsync([userId], cancellationToken).ConfigureAwait(false);

            await _audit.RecordAsync(
                    AuditEvent.Succeeded(
                        AccessAuditActions.OrganizationMemberRemoved,
                        await _directory.UserSubjectAsync(userId, cancellationToken).ConfigureAwait(false),
                        AccessAudit.Details(("organization", organization.Name), ("organizationId", organization.Id.ToString()))),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return Result<OrganizationDetailDto>.Success(await ToDetailAsync(organization, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<IReadOnlyList<OrganizationMembershipDto>>> ListForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var guard = await _authorization.EnsureCanAsync(AccessOperation.ListOrganizations, cancellationToken).ConfigureAwait(false);

        return guard.IsFailure
            ? Result<IReadOnlyList<OrganizationMembershipDto>>.Failure(guard.Error)
            : Result<IReadOnlyList<OrganizationMembershipDto>>.Success(
                await OrganizationQueries.MembershipsAsync(_db, userId, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<IReadOnlyList<OrganizationMembershipDto>>> ListMineAsync(CancellationToken cancellationToken) =>
        _caller.UserId is Guid userId
            ? Result<IReadOnlyList<OrganizationMembershipDto>>.Success(
                await OrganizationQueries.MembershipsAsync(_db, userId, cancellationToken).ConfigureAwait(false))
            : Result<IReadOnlyList<OrganizationMembershipDto>>.Failure(AccessErrors.SignInRequired);

    /// <summary>
    /// Ends the sessions the users hold in the applications that work per organization, so a token naming an
    /// organization they left cannot be refreshed into one more access token. Their sessions elsewhere stay.
    /// </summary>
    private async Task RevokeOrganizationSessionsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return;
        }

        var clientIds = await _db.Applications
            .AsNoTracking()
            .Where(application => application.Organizations != OrganizationMode.None)
            .Select(application => application.ClientId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var clientId in clientIds)
        {
            await _clients.RevokeAsync(clientId, userIds, cancellationToken).ConfigureAwait(false);
        }
    }

    private Task<Organization?> FindAsync(Guid organizationId, CancellationToken cancellationToken) =>
        _db.Organizations
            .Include(organization => organization.Members)
            .SingleOrDefaultAsync(organization => organization.Id == organizationId, cancellationToken);

    private async Task<OrganizationDetailDto> ToDetailAsync(Organization organization, CancellationToken cancellationToken)
    {
        var resolve = await AccessQueries.ResolveUsersAsync(
                _directory,
                organization.Members.Select(member => member.UserId),
                cancellationToken)
            .ConfigureAwait(false);

        return new OrganizationDetailDto(
            organization.Id,
            organization.Name,
            organization.Slug,
            organization.Description,
            organization.Members
                .Select(member => (User: resolve(member.UserId), member))
                .OrderBy(row => row.User.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(row => new OrganizationMemberDto(
                    row.User.Id,
                    row.User.Email,
                    row.User.DisplayName,
                    row.User.IsKnown,
                    ToContract(row.member.Role),
                    row.member.AddedAtUtc))
                .ToList(),
            organization.CreatedAtUtc,
            organization.UpdatedAtUtc);
    }

    public static string ToContract(OrganizationRole role) => role switch
    {
        OrganizationRole.Member => OrganizationRoles.Member,
        OrganizationRole.Admin => OrganizationRoles.Admin,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
    };

    private static OrganizationRole? ParseRole(string? role) => role?.Trim().ToLowerInvariant() switch
    {
        OrganizationRoles.Member => OrganizationRole.Member,
        OrganizationRoles.Admin => OrganizationRole.Admin,
        _ => null,
    };
}

/// <summary>The organization reads shared by the administration and the token endpoints.</summary>
internal static class OrganizationQueries
{
    /// <summary>The organizations the user belongs to, by name.</summary>
    public static async Task<IReadOnlyList<OrganizationMembershipDto>> MembershipsAsync(
        AccessDbContext db,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var rows = await db.OrganizationMembers
            .AsNoTracking()
            .Where(member => member.UserId == userId)
            .Join(db.Organizations, member => member.OrganizationId, organization => organization.Id, (member, organization) => new
            {
                organization.Id,
                organization.Slug,
                organization.Name,
                member.Role,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .Select(row => new OrganizationMembershipDto(row.Id, row.Slug, row.Name, OrganizationService.ToContract(row.Role)))
            .ToList();
    }
}
