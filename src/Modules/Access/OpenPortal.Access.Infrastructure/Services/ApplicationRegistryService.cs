using Microsoft.EntityFrameworkCore;
using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Access.Application.Auditing;
using OpenPortal.Access.Application.Contracts;
using OpenPortal.Access.Domain;
using OpenPortal.Access.Domain.Applications;
using OpenPortal.Access.Infrastructure.Persistence;
using OpenPortal.SharedKernel.Auditing;
using OpenPortal.SharedKernel.Results;
using OpenPortal.SharedKernel.Time;

namespace OpenPortal.Access.Infrastructure.Services;

/// <summary>Administration of the applications that sign in through the portal.</summary>
internal sealed class ApplicationRegistryService : IApplicationRegistryService
{
    private readonly AccessDbContext _db;
    private readonly OidcClientRegistry _clients;
    private readonly IAccessAdminAuthorization _authorization;
    private readonly IClock _clock;
    private readonly IAuditTrail _audit;

    public ApplicationRegistryService(
        AccessDbContext db,
        OidcClientRegistry clients,
        IAccessAdminAuthorization authorization,
        IClock clock,
        IAuditTrail audit)
    {
        _db = db;
        _clients = clients;
        _authorization = authorization;
        _clock = clock;
        _audit = audit;
    }

    public async Task<Result<IReadOnlyList<ApplicationDto>>> ListAsync(CancellationToken cancellationToken)
    {
        var guard = await _authorization.EnsureCanAsync(AccessOperation.ListApplications, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<IReadOnlyList<ApplicationDto>>.Failure(guard.Error);
        }

        var applications = await _db.Applications.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);

        var userCounts = await _db.UserGrants
            .GroupBy(grant => grant.ApplicationId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.Key, row => row.Count, cancellationToken)
            .ConfigureAwait(false);

        var groupCounts = await _db.GroupGrants
            .GroupBy(grant => grant.ApplicationId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.Key, row => row.Count, cancellationToken)
            .ConfigureAwait(false);

        // Waiting applications first: they are the ones that need an administrator.
        IReadOnlyList<ApplicationDto> result = applications
            .OrderBy(application => application.Status == ApplicationStatus.Pending ? 0 : 1)
            .ThenBy(application => application.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(application => application.ToDto(
                userCounts.GetValueOrDefault(application.Id),
                groupCounts.GetValueOrDefault(application.Id)))
            .ToList();

        return Result<IReadOnlyList<ApplicationDto>>.Success(result);
    }

    public async Task<Result<ApplicationDto>> GetAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        var guard = await _authorization.EnsureCanAsync(AccessOperation.ManageApplications, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<ApplicationDto>.Failure(guard.Error);
        }

        var application = await FindAsync(applicationId, cancellationToken).ConfigureAwait(false);

        return application is null
            ? Result<ApplicationDto>.Failure(AccessErrors.ApplicationNotFound)
            : Result<ApplicationDto>.Success(await ToDtoAsync(application, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<ApplicationSecretDto>> CreateAsync(
        CreateApplicationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var guard = await _authorization.EnsureCanAsync(AccessOperation.ManageApplications, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<ApplicationSecretDto>.Failure(guard.Error);
        }

        var now = _clock.UtcNow;
        var created = PortalApplication.CreateManual(
            Guid.NewGuid(),
            request.ClientId,
            new ApplicationDetails(
                request.DisplayName,
                request.Description,
                request.BaseUrl,
                request.RedirectUris ?? [],
                request.PostLogoutRedirectUris ?? []),
            now);

        if (created.IsFailure)
        {
            return Result<ApplicationSecretDto>.Failure(created.Error);
        }

        var application = created.Value;

        var configured = Configure(application, request.Roles, request.GroupClaims ?? GroupClaimModes.None, now);
        if (configured.IsFailure)
        {
            return Result<ApplicationSecretDto>.Failure(configured.Error);
        }

        if (await ClientIdInUseAsync(application.ClientId, cancellationToken).ConfigureAwait(false))
        {
            return Result<ApplicationSecretDto>.Failure(AccessErrors.ClientIdInUse);
        }

        // The application row and the OpenIddict client commit together or not at all.
        string secret;
        await using (var transaction = await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false))
        {
            _db.Applications.Add(application);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            secret = await _clients.IssueSecretAsync(application, cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        await _audit.RecordAsync(AuditEvent.Succeeded(AccessAuditActions.ApplicationCreated, application.ToAuditSubject()), cancellationToken)
            .ConfigureAwait(false);

        return Result<ApplicationSecretDto>.Success(new ApplicationSecretDto(application.ToDto(0, 0), secret));
    }

    public async Task<Result<ApplicationDto>> UpdateAsync(
        Guid applicationId,
        UpdateApplicationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var guard = await _authorization.EnsureCanAsync(AccessOperation.ManageApplications, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<ApplicationDto>.Failure(guard.Error);
        }

        var application = await FindAsync(applicationId, cancellationToken).ConfigureAwait(false);
        if (application is null)
        {
            return Result<ApplicationDto>.Failure(AccessErrors.ApplicationNotFound);
        }

        var now = _clock.UtcNow;
        var rolesBefore = application.Roles.Select(role => (role.Key, role.DisplayName, role.Description)).ToList();

        var updated = application.Update(
            new ApplicationDetails(
                request.DisplayName,
                request.Description,
                request.BaseUrl,
                request.RedirectUris ?? [],
                request.PostLogoutRedirectUris ?? []),
            now);

        if (updated.IsFailure)
        {
            return Result<ApplicationDto>.Failure(updated.Error);
        }

        var configured = Configure(application, request.Roles, request.GroupClaims, now);
        if (configured.IsFailure)
        {
            return Result<ApplicationDto>.Failure(configured.Error);
        }

        await using (var transaction = await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false))
        {
            // A role that is gone takes its assignments with it, so it cannot come back to life on the grants
            // if a role with the same key is defined again later.
            if (configured.Value.Count > 0)
            {
                await RemoveRoleAssignmentsAsync(application.Id, configured.Value, cancellationToken).ConfigureAwait(false);
            }

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await _clients.SyncAsync(application, cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        await _audit.RecordAsync(AuditEvent.Succeeded(AccessAuditActions.ApplicationUpdated, application.ToAuditSubject()), cancellationToken)
            .ConfigureAwait(false);

        var rolesAfter = application.Roles.Select(role => (role.Key, role.DisplayName, role.Description)).ToList();
        if (!rolesBefore.OrderBy(role => role.Key, StringComparer.Ordinal).SequenceEqual(rolesAfter.OrderBy(role => role.Key, StringComparer.Ordinal)))
        {
            await _audit.RecordAsync(
                    AuditEvent.Succeeded(
                        AccessAuditActions.ApplicationRolesChanged,
                        application.ToAuditSubject(),
                        AccessAudit.Details(
                            ("roles", AccessAudit.JoinRoles(rolesAfter.Select(role => role.Key).Order(StringComparer.Ordinal).ToList())),
                            ("removed", configured.Value.Count > 0 ? AccessAudit.JoinRoles(configured.Value) : null))),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return Result<ApplicationDto>.Success(await ToDtoAsync(application, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<ApplicationSecretDto>> ApproveAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        var guard = await _authorization.EnsureCanAsync(AccessOperation.ManageApplications, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<ApplicationSecretDto>.Failure(guard.Error);
        }

        var application = await FindAsync(applicationId, cancellationToken).ConfigureAwait(false);
        if (application is null)
        {
            return Result<ApplicationSecretDto>.Failure(AccessErrors.ApplicationNotFound);
        }

        var approved = application.Approve(_clock.UtcNow);
        if (approved.IsFailure)
        {
            return Result<ApplicationSecretDto>.Failure(approved.Error);
        }

        string secret;
        await using (var transaction = await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false))
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            secret = await _clients.IssueSecretAsync(application, cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        await _audit.RecordAsync(AuditEvent.Succeeded(AccessAuditActions.ApplicationApproved, application.ToAuditSubject()), cancellationToken)
            .ConfigureAwait(false);

        return Result<ApplicationSecretDto>.Success(
            new ApplicationSecretDto(await ToDtoAsync(application, cancellationToken).ConfigureAwait(false), secret));
    }

    public async Task<Result<ApplicationSecretDto>> RegenerateSecretAsync(
        Guid applicationId,
        CancellationToken cancellationToken)
    {
        var guard = await _authorization.EnsureCanAsync(AccessOperation.ManageApplications, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<ApplicationSecretDto>.Failure(guard.Error);
        }

        var application = await FindAsync(applicationId, cancellationToken).ConfigureAwait(false);
        if (application is null)
        {
            return Result<ApplicationSecretDto>.Failure(AccessErrors.ApplicationNotFound);
        }

        if (application.Status == ApplicationStatus.Pending)
        {
            return Result<ApplicationSecretDto>.Failure(AccessErrors.ApplicationNotActive);
        }

        var secret = await _clients.IssueSecretAsync(application, cancellationToken).ConfigureAwait(false);

        await _audit.RecordAsync(
                AuditEvent.Succeeded(AccessAuditActions.ApplicationSecretRegenerated, application.ToAuditSubject()),
                cancellationToken)
            .ConfigureAwait(false);

        return Result<ApplicationSecretDto>.Success(
            new ApplicationSecretDto(await ToDtoAsync(application, cancellationToken).ConfigureAwait(false), secret));
    }

    public Task<Result<ApplicationDto>> ApplyManifestAsync(Guid applicationId, CancellationToken cancellationToken) =>
        ChangeAsync(
            applicationId,
            application => application.ApplyAnnouncedManifest(_clock.UtcNow),
            (application, token) => _clients.SyncAsync(application, token),
            AccessAuditActions.ApplicationManifestApplied,
            cancellationToken);

    public Task<Result<ApplicationDto>> DisableAsync(Guid applicationId, CancellationToken cancellationToken) =>
        ChangeAsync(
            applicationId,
            application => application.Disable(_clock.UtcNow),
            (application, token) => _clients.RevokeAsync(application.ClientId, userIds: null, token),
            AccessAuditActions.ApplicationDisabled,
            cancellationToken);

    public Task<Result<ApplicationDto>> EnableAsync(Guid applicationId, CancellationToken cancellationToken) =>
        ChangeAsync(
            applicationId,
            application => application.Enable(_clock.UtcNow),
            afterSave: null,
            AccessAuditActions.ApplicationEnabled,
            cancellationToken);

    public async Task<Result> DeleteAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        var guard = await _authorization.EnsureCanAsync(AccessOperation.ManageApplications, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return guard;
        }

        var application = await FindAsync(applicationId, cancellationToken).ConfigureAwait(false);
        if (application is null)
        {
            return Result.Failure(AccessErrors.ApplicationNotFound);
        }

        var subject = application.ToAuditSubject();

        await using (var transaction = await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false))
        {
            await _clients.DeleteAsync(application.ClientId, cancellationToken).ConfigureAwait(false);

            // Grants and roles go with the application through the cascading foreign keys.
            _db.Applications.Remove(application);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        await _audit.RecordAsync(AuditEvent.Succeeded(AccessAuditActions.ApplicationDeleted, subject), cancellationToken)
            .ConfigureAwait(false);

        return Result.Success();
    }

    /// <summary>
    /// Applies the roles and the group claim setting of a create or update request. Null leaves each as it is.
    /// Returns the keys of the roles that were removed.
    /// </summary>
    private static Result<IReadOnlyList<string>> Configure(
        PortalApplication application,
        IReadOnlyList<ApplicationRoleDto>? roles,
        string? groupClaims,
        DateTimeOffset now)
    {
        if (groupClaims is not null)
        {
            if (AccessMapping.ParseGroupClaims(groupClaims) is not { } mode)
            {
                return Result<IReadOnlyList<string>>.Failure(AccessErrors.GroupClaimsInvalid);
            }

            application.SetGroupClaims(mode, now);
        }

        return roles is null
            ? Result<IReadOnlyList<string>>.Success([])
            : application.SetRoles(roles.ToDetails(), now);
    }

    /// <summary>Strips removed role keys from every grant on the application.</summary>
    private async Task RemoveRoleAssignmentsAsync(Guid applicationId, IReadOnlyList<string> removed, CancellationToken cancellationToken)
    {
        var userGrants = await _db.UserGrants
            .Where(grant => grant.ApplicationId == applicationId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var grant in userGrants)
        {
            grant.SetRoles(grant.Roles.Except(removed, StringComparer.Ordinal));
        }

        var groupGrants = await _db.GroupGrants
            .Where(grant => grant.ApplicationId == applicationId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var grant in groupGrants)
        {
            grant.SetRoles(grant.Roles.Except(removed, StringComparer.Ordinal));
        }
    }

    /// <summary>
    /// Loads, mutates through the domain, saves, then runs the OpenIddict side effect. A rejected mutation
    /// leaves both stores untouched. The change is audited once it is committed.
    /// </summary>
    private async Task<Result<ApplicationDto>> ChangeAsync(
        Guid applicationId,
        Func<PortalApplication, Result> mutate,
        Func<PortalApplication, CancellationToken, Task>? afterSave,
        string auditAction,
        CancellationToken cancellationToken)
    {
        var guard = await _authorization.EnsureCanAsync(AccessOperation.ManageApplications, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<ApplicationDto>.Failure(guard.Error);
        }

        var application = await FindAsync(applicationId, cancellationToken).ConfigureAwait(false);
        if (application is null)
        {
            return Result<ApplicationDto>.Failure(AccessErrors.ApplicationNotFound);
        }

        var changed = mutate(application);
        if (changed.IsFailure)
        {
            return Result<ApplicationDto>.Failure(changed.Error);
        }

        await using (var transaction = await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false))
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            if (afterSave is not null)
            {
                await afterSave(application, cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        await _audit.RecordAsync(AuditEvent.Succeeded(auditAction, application.ToAuditSubject()), cancellationToken).ConfigureAwait(false);

        return Result<ApplicationDto>.Success(await ToDtoAsync(application, cancellationToken).ConfigureAwait(false));
    }

    private Task<PortalApplication?> FindAsync(Guid applicationId, CancellationToken cancellationToken) =>
        _db.Applications.SingleOrDefaultAsync(application => application.Id == applicationId, cancellationToken);

    private Task<bool> ClientIdInUseAsync(string clientId, CancellationToken cancellationToken) =>
        _db.Applications.AnyAsync(application => application.ClientId == clientId, cancellationToken);

    private async Task<ApplicationDto> ToDtoAsync(PortalApplication application, CancellationToken cancellationToken)
    {
        var users = await _db.UserGrants
            .CountAsync(grant => grant.ApplicationId == application.Id, cancellationToken)
            .ConfigureAwait(false);

        var groups = await _db.GroupGrants
            .CountAsync(grant => grant.ApplicationId == application.Id, cancellationToken)
            .ConfigureAwait(false);

        return application.ToDto(users, groups);
    }
}
