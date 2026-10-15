using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenPortal.Access.Infrastructure.Persistence;
using OpenPortal.SharedKernel.Privacy;

namespace OpenPortal.Access.Infrastructure.Services;

/// <summary>
/// The access section of a personal data export: groups, organizations, direct grants, access requests and the
/// applications the person signed in to (OpenID Connect authorizations). Tokens themselves are never exported.
/// </summary>
internal sealed class AccessPersonalData : IPersonalDataSource
{
    private readonly AccessDbContext _db;
    private readonly IOpenIddictAuthorizationManager _authorizations;
    private readonly IOpenIddictApplicationManager _clients;

    public AccessPersonalData(
        AccessDbContext db,
        IOpenIddictAuthorizationManager authorizations,
        IOpenIddictApplicationManager clients)
    {
        _db = db;
        _authorizations = authorizations;
        _clients = clients;
    }

    public string Section => "access";

    public async Task<object> ExportAsync(Guid userId, CancellationToken cancellationToken)
    {
        var applications = await _db.Applications
            .AsNoTracking()
            .ToDictionaryAsync(application => application.Id, application => application.DisplayName, cancellationToken)
            .ConfigureAwait(false);

        var groups = await _db.GroupMembers
            .AsNoTracking()
            .Where(member => member.UserId == userId)
            .Join(_db.Groups.AsNoTracking(), member => member.GroupId, group => group.Id, (member, group) => new { group.Name, member.AddedAtUtc })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var organizations = await _db.OrganizationMembers
            .AsNoTracking()
            .Where(member => member.UserId == userId)
            .Join(
                _db.Organizations.AsNoTracking(),
                member => member.OrganizationId,
                organization => organization.Id,
                (member, organization) => new { organization.Name, organization.Slug, member.Role, member.AddedAtUtc })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var grants = await _db.UserGrants
            .AsNoTracking()
            .Where(grant => grant.UserId == userId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var requests = await _db.AccessRequests
            .AsNoTracking()
            .Where(request => request.UserId == userId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var signIns = new List<object>();
        await foreach (var authorization in _authorizations.FindBySubjectAsync(userId.ToString(), cancellationToken).ConfigureAwait(false))
        {
            var clientKey = await _authorizations.GetApplicationIdAsync(authorization, cancellationToken).ConfigureAwait(false);
            var client = clientKey is null ? null : await _clients.FindByIdAsync(clientKey, cancellationToken).ConfigureAwait(false);

            signIns.Add(new
            {
                application = client is null ? null : await _clients.GetDisplayNameAsync(client, cancellationToken).ConfigureAwait(false),
                clientId = client is null ? null : await _clients.GetClientIdAsync(client, cancellationToken).ConfigureAwait(false),
                createdAtUtc = await _authorizations.GetCreationDateAsync(authorization, cancellationToken).ConfigureAwait(false),
                status = await _authorizations.GetStatusAsync(authorization, cancellationToken).ConfigureAwait(false),
                scopes = await _authorizations.GetScopesAsync(authorization, cancellationToken).ConfigureAwait(false),
            });
        }

        return new
        {
            groups = groups.Select(group => new { group.Name, group.AddedAtUtc }).ToList(),
            organizations = organizations
                .Select(organization => new
                {
                    organization.Name,
                    organization.Slug,
                    role = organization.Role.ToString().ToLowerInvariant(),
                    organization.AddedAtUtc,
                })
                .ToList(),
            applicationGrants = grants
                .Select(grant => new
                {
                    application = applications.GetValueOrDefault(grant.ApplicationId),
                    grant.Roles,
                    grant.GrantedAtUtc,
                })
                .ToList(),
            accessRequests = requests
                .OrderBy(request => request.CreatedAtUtc)
                .Select(request => new
                {
                    application = applications.GetValueOrDefault(request.ApplicationId),
                    status = request.Status.ToString().ToLowerInvariant(),
                    request.Reason,
                    request.CreatedAtUtc,
                    request.DecidedAtUtc,
                    request.DecisionNote,
                    request.GrantedRoles,
                })
                .ToList(),
            applicationSignIns = signIns,
        };
    }
}
