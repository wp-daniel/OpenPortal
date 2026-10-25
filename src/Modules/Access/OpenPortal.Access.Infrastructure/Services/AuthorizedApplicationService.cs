using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Access.Application.Auditing;
using OpenPortal.Access.Application.Contracts;
using OpenPortal.Access.Domain;
using OpenPortal.Access.Infrastructure.Persistence;
using OpenPortal.SharedKernel.Auditing;
using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Access.Infrastructure.Services;

/// <inheritdoc />
internal sealed class AuthorizedApplicationService : IAuthorizedApplicationService
{
    private readonly AccessDbContext _db;
    private readonly IOpenIddictAuthorizationManager _authorizations;
    private readonly IOpenIddictApplicationManager _applications;
    private readonly OidcClientRegistry _clients;
    private readonly IAccessCaller _caller;
    private readonly IAuditTrail _audit;

    public AuthorizedApplicationService(
        AccessDbContext db,
        IOpenIddictAuthorizationManager authorizations,
        IOpenIddictApplicationManager applications,
        OidcClientRegistry clients,
        IAccessCaller caller,
        IAuditTrail audit)
    {
        _db = db;
        _authorizations = authorizations;
        _applications = applications;
        _clients = clients;
        _caller = caller;
        _audit = audit;
    }

    public async Task<Result<IReadOnlyList<AuthorizedApplicationDto>>> ListMineAsync(CancellationToken cancellationToken)
    {
        if (_caller.UserId is not Guid userId)
        {
            return Result<IReadOnlyList<AuthorizedApplicationDto>>.Failure(AccessErrors.SignInRequired);
        }

        // One entry per application: its lasting authorizations together (the scopes of all, the oldest date).
        var byClient = new Dictionary<string, (HashSet<string> Scopes, DateTimeOffset? Since)>(StringComparer.Ordinal);

        await foreach (var authorization in _authorizations.FindBySubjectAsync(userId.ToString(), cancellationToken).ConfigureAwait(false))
        {
            if (await _authorizations.GetStatusAsync(authorization, cancellationToken).ConfigureAwait(false) != OpenIddictConstants.Statuses.Valid
                || await _authorizations.GetApplicationIdAsync(authorization, cancellationToken).ConfigureAwait(false) is not { } applicationKey
                || await _applications.FindByIdAsync(applicationKey, cancellationToken).ConfigureAwait(false) is not { } client
                || await _applications.GetClientIdAsync(client, cancellationToken).ConfigureAwait(false) is not { } clientId)
            {
                continue;
            }

            var scopes = await _authorizations.GetScopesAsync(authorization, cancellationToken).ConfigureAwait(false);
            var created = await _authorizations.GetCreationDateAsync(authorization, cancellationToken).ConfigureAwait(false);

            var entry = byClient.TryGetValue(clientId, out var existing) ? existing : ([], null);
            entry.Scopes.UnionWith(scopes);
            entry.Since = entry.Since is null || (created is not null && created < entry.Since) ? created : entry.Since;
            byClient[clientId] = entry;
        }

        var clientIds = byClient.Keys.ToList();
        var applications = await _db.Applications
            .AsNoTracking()
            .Where(application => clientIds.Contains(application.ClientId))
            .Select(application => new { application.ClientId, application.DisplayName, application.BaseUrl })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<AuthorizedApplicationDto> result = applications
            .Select(application => new AuthorizedApplicationDto(
                application.ClientId,
                application.DisplayName,
                application.BaseUrl,
                byClient[application.ClientId].Scopes.Order(StringComparer.Ordinal).ToList(),
                byClient[application.ClientId].Since))
            .OrderBy(application => application.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Result<IReadOnlyList<AuthorizedApplicationDto>>.Success(result);
    }

    public async Task<Result> RevokeMineAsync(string clientId, CancellationToken cancellationToken)
    {
        if (_caller.UserId is not Guid userId)
        {
            return Result.Failure(AccessErrors.SignInRequired);
        }

        var application = await _db.Applications
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.ClientId == clientId, cancellationToken)
            .ConfigureAwait(false);
        if (application is null)
        {
            return Result.Failure(AccessErrors.ApplicationNotFound);
        }

        // The grant (whether the user may open the application) stays: this only ends what the application holds.
        await _clients.RevokeAsync(clientId, [userId], cancellationToken).ConfigureAwait(false);

        await _audit.RecordAsync(AuditEvent.Succeeded(AccessAuditActions.AuthorizationRevoked, application.ToAuditSubject()), cancellationToken)
            .ConfigureAwait(false);

        return Result.Success();
    }
}
