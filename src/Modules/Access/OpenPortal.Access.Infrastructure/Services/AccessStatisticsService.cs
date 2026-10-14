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

/// <summary>Counts of what the Access module holds, for the statistics page.</summary>
internal sealed class AccessStatisticsService : IAccessStatisticsService
{
    private readonly AccessDbContext _db;
    private readonly IAccessAdminAuthorization _authorization;

    public AccessStatisticsService(AccessDbContext db, IAccessAdminAuthorization authorization)
    {
        _db = db;
        _authorization = authorization;
    }

    public async Task<Result<AccessStatisticsDto>> GetAsync(CancellationToken cancellationToken)
    {
        var guard = await _authorization.EnsureCanAsync(AccessOperation.ViewStatistics, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<AccessStatisticsDto>.Failure(guard.Error);
        }

        var statuses = await _db.Applications
            .AsNoTracking()
            .GroupBy(application => application.Status)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.Key, row => row.Count, cancellationToken)
            .ConfigureAwait(false);

        return Result<AccessStatisticsDto>.Success(new AccessStatisticsDto(
            Applications: statuses.Values.Sum(),
            ActiveApplications: statuses.GetValueOrDefault(ApplicationStatus.Active),
            PendingApplications: statuses.GetValueOrDefault(ApplicationStatus.Pending),
            DisabledApplications: statuses.GetValueOrDefault(ApplicationStatus.Disabled),
            Groups: await _db.Groups.CountAsync(cancellationToken).ConfigureAwait(false),
            Organizations: await _db.Organizations.CountAsync(cancellationToken).ConfigureAwait(false),
            UserGrants: await _db.UserGrants.CountAsync(cancellationToken).ConfigureAwait(false),
            GroupGrants: await _db.GroupGrants.CountAsync(cancellationToken).ConfigureAwait(false),
            PendingAccessRequests: await _db.AccessRequests
                .CountAsync(request => request.Status == AccessRequestStatus.Pending, cancellationToken)
                .ConfigureAwait(false)));
    }
}
