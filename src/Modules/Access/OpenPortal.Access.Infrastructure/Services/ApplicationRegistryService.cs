using Microsoft.EntityFrameworkCore;
using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Access.Application.Contracts;
using OpenPortal.Access.Domain;
using OpenPortal.Access.Domain.Applications;
using OpenPortal.Access.Infrastructure.Persistence;
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

    public ApplicationRegistryService(
        AccessDbContext db,
        OidcClientRegistry clients,
        IAccessAdminAuthorization authorization,
        IClock clock)
    {
        _db = db;
        _clients = clients;
        _authorization = authorization;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyList<ApplicationDto>>> ListAsync(CancellationToken cancellationToken)
    {
        var guard = _authorization.EnsureCanAdminister();
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
        var guard = _authorization.EnsureCanAdminister();
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

        var guard = _authorization.EnsureCanAdminister();
        if (guard.IsFailure)
        {
            return Result<ApplicationSecretDto>.Failure(guard.Error);
        }

        var created = PortalApplication.CreateManual(
            Guid.NewGuid(),
            request.ClientId,
            new ApplicationDetails(
                request.DisplayName,
                request.Description,
                request.BaseUrl,
                request.RedirectUris ?? [],
                request.PostLogoutRedirectUris ?? []),
            _clock.UtcNow);

        if (created.IsFailure)
        {
            return Result<ApplicationSecretDto>.Failure(created.Error);
        }

        var application = created.Value;

        if (await ClientIdInUseAsync(application.ClientId, cancellationToken).ConfigureAwait(false))
        {
            return Result<ApplicationSecretDto>.Failure(AccessErrors.ClientIdInUse);
        }

        // The application row and the OpenIddict client commit together or not at all.
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        _db.Applications.Add(application);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var secret = await _clients.IssueSecretAsync(application, cancellationToken).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result<ApplicationSecretDto>.Success(new ApplicationSecretDto(application.ToDto(0, 0), secret));
    }

    public async Task<Result<ApplicationDto>> UpdateAsync(
        Guid applicationId,
        UpdateApplicationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await ChangeAsync(
            applicationId,
            application => application.Update(
                new ApplicationDetails(
                    request.DisplayName,
                    request.Description,
                    request.BaseUrl,
                    request.RedirectUris ?? [],
                    request.PostLogoutRedirectUris ?? []),
                _clock.UtcNow),
            (application, token) => _clients.SyncAsync(application, token),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<ApplicationSecretDto>> ApproveAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        var guard = _authorization.EnsureCanAdminister();
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

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var secret = await _clients.IssueSecretAsync(application, cancellationToken).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result<ApplicationSecretDto>.Success(
            new ApplicationSecretDto(await ToDtoAsync(application, cancellationToken).ConfigureAwait(false), secret));
    }

    public async Task<Result<ApplicationSecretDto>> RegenerateSecretAsync(
        Guid applicationId,
        CancellationToken cancellationToken)
    {
        var guard = _authorization.EnsureCanAdminister();
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

        return Result<ApplicationSecretDto>.Success(
            new ApplicationSecretDto(await ToDtoAsync(application, cancellationToken).ConfigureAwait(false), secret));
    }

    public Task<Result<ApplicationDto>> ApplyManifestAsync(Guid applicationId, CancellationToken cancellationToken) =>
        ChangeAsync(
            applicationId,
            application => application.ApplyAnnouncedManifest(_clock.UtcNow),
            (application, token) => _clients.SyncAsync(application, token),
            cancellationToken);

    public Task<Result<ApplicationDto>> DisableAsync(Guid applicationId, CancellationToken cancellationToken) =>
        ChangeAsync(
            applicationId,
            application => application.Disable(_clock.UtcNow),
            (application, token) => _clients.RevokeAsync(application.ClientId, userIds: null, token),
            cancellationToken);

    public Task<Result<ApplicationDto>> EnableAsync(Guid applicationId, CancellationToken cancellationToken) =>
        ChangeAsync(
            applicationId,
            application => application.Enable(_clock.UtcNow),
            afterSave: null,
            cancellationToken);

    public async Task<Result> DeleteAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        var guard = _authorization.EnsureCanAdminister();
        if (guard.IsFailure)
        {
            return guard;
        }

        var application = await FindAsync(applicationId, cancellationToken).ConfigureAwait(false);
        if (application is null)
        {
            return Result.Failure(AccessErrors.ApplicationNotFound);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await _clients.DeleteAsync(application.ClientId, cancellationToken).ConfigureAwait(false);

        // Grants go with the application through the cascading foreign keys.
        _db.Applications.Remove(application);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    /// <summary>
    /// Loads, mutates through the domain, saves, then runs the OpenIddict side effect. A rejected mutation
    /// leaves both stores untouched.
    /// </summary>
    private async Task<Result<ApplicationDto>> ChangeAsync(
        Guid applicationId,
        Func<PortalApplication, Result> mutate,
        Func<PortalApplication, CancellationToken, Task>? afterSave,
        CancellationToken cancellationToken)
    {
        var guard = _authorization.EnsureCanAdminister();
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

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (afterSave is not null)
        {
            await afterSave(application, cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

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
