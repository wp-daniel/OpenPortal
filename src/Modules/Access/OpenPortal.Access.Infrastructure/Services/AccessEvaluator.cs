using Microsoft.EntityFrameworkCore;
using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Access.Application.Contracts;
using OpenPortal.Access.Domain.Applications;
using OpenPortal.Access.Infrastructure.Persistence;

namespace OpenPortal.Access.Infrastructure.Services;

/// <summary>The access check behind every sign-in and token refresh, and the user's launchpad.</summary>
internal sealed class AccessEvaluator : IAccessEvaluator
{
    private readonly AccessDbContext _db;
    private readonly AccessQueries _access;

    public AccessEvaluator(AccessDbContext db, AccessQueries access)
    {
        _db = db;
        _access = access;
    }

    public async Task<AccessDecision> EvaluateAsync(Guid userId, string clientId, CancellationToken cancellationToken)
    {
        var application = await _db.Applications
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.ClientId == clientId, cancellationToken)
            .ConfigureAwait(false);

        if (application is null)
        {
            return new AccessDecision(Allowed: false, ApplicationName: null);
        }

        var allowed = application.Status == ApplicationStatus.Active
            && await _access.HasGrantAsync(application.Id, userId, cancellationToken).ConfigureAwait(false);

        return new AccessDecision(allowed, application.DisplayName);
    }

    public async Task<IReadOnlyList<LaunchpadItemDto>> GetLaunchpadAsync(Guid userId, CancellationToken cancellationToken)
    {
        var granted = await _access.GrantedApplicationIdsAsync(userId, cancellationToken).ConfigureAwait(false);

        if (granted.Count == 0)
        {
            return [];
        }

        var applications = await _db.Applications
            .AsNoTracking()
            .Where(application => granted.Contains(application.Id) && application.Status == ApplicationStatus.Active)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return applications
            .OrderBy(application => application.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(application => new LaunchpadItemDto(
                application.Id,
                application.ClientId,
                application.DisplayName,
                application.Description,
                application.BaseUrl))
            .ToList();
    }
}
