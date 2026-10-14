using Microsoft.EntityFrameworkCore;
using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Access.Application.Auditing;
using OpenPortal.Access.Application.Contracts;
using OpenPortal.Access.Application.Notifications;
using OpenPortal.Access.Domain;
using OpenPortal.Access.Domain.Applications;
using OpenPortal.Access.Domain.Requests;
using OpenPortal.Access.Infrastructure.Persistence;
using OpenPortal.SharedKernel.Auditing;
using OpenPortal.SharedKernel.Notifications;
using OpenPortal.SharedKernel.Results;
using OpenPortal.SharedKernel.Time;

namespace OpenPortal.Access.Infrastructure.Services;

/// <summary>
/// Users asking for applications and reviewers deciding. Approving creates an ordinary user grant through
/// <see cref="UserGrantOperations"/>, so a granted request is withdrawn like any other grant.
/// </summary>
internal sealed class AccessRequestService : IAccessRequestService
{
    private readonly AccessDbContext _db;
    private readonly AccessQueries _access;
    private readonly UserGrantOperations _grants;
    private readonly IAccessCaller _caller;
    private readonly IAccessAdminAuthorization _authorization;
    private readonly IUserDirectory _directory;
    private readonly IClock _clock;
    private readonly IAuditTrail _audit;
    private readonly INotifier _notifier;

    public AccessRequestService(
        AccessDbContext db,
        AccessQueries access,
        UserGrantOperations grants,
        IAccessCaller caller,
        IAccessAdminAuthorization authorization,
        IUserDirectory directory,
        IClock clock,
        IAuditTrail audit,
        INotifier notifier)
    {
        _db = db;
        _access = access;
        _grants = grants;
        _caller = caller;
        _authorization = authorization;
        _directory = directory;
        _clock = clock;
        _audit = audit;
        _notifier = notifier;
    }

    // -----------------------------------------------------------------------
    // The requester's side.
    // -----------------------------------------------------------------------

    public async Task<Result<IReadOnlyList<RequestableApplicationDto>>> GetCatalogAsync(CancellationToken cancellationToken)
    {
        if (_caller.UserId is not Guid userId)
        {
            return Result<IReadOnlyList<RequestableApplicationDto>>.Failure(AccessErrors.SignInRequired);
        }

        var granted = await _access.GrantedApplicationIdsAsync(userId, cancellationToken).ConfigureAwait(false);

        var applications = await _db.Applications
            .AsNoTracking()
            .Where(application => application.Status == ApplicationStatus.Active && application.AllowAccessRequests)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var requestable = applications.Where(application => !granted.Contains(application.Id)).ToList();
        var ids = requestable.Select(application => application.Id).ToArray();

        var requests = await _db.AccessRequests
            .AsNoTracking()
            .Where(request => request.UserId == userId && ids.Contains(request.ApplicationId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var latest = requests
            .GroupBy(request => request.ApplicationId)
            .ToDictionary(group => group.Key, group => group.MaxBy(request => request.CreatedAtUtc)!);

        IReadOnlyList<RequestableApplicationDto> catalog = requestable
            .OrderBy(application => application.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(application => new RequestableApplicationDto(
                application.Id,
                application.ClientId,
                application.DisplayName,
                application.Description,
                latest.TryGetValue(application.Id, out var request) ? ToMine(request, application.DisplayName) : null))
            .ToList();

        return Result<IReadOnlyList<RequestableApplicationDto>>.Success(catalog);
    }

    public async Task<Result<IReadOnlyList<MyAccessRequestDto>>> ListMineAsync(CancellationToken cancellationToken)
    {
        if (_caller.UserId is not Guid userId)
        {
            return Result<IReadOnlyList<MyAccessRequestDto>>.Failure(AccessErrors.SignInRequired);
        }

        var rows = await _db.AccessRequests
            .AsNoTracking()
            .Where(request => request.UserId == userId)
            .Join(_db.Applications, request => request.ApplicationId, application => application.Id, (request, application) => new { request, application.DisplayName })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<MyAccessRequestDto> mine = rows
            .OrderByDescending(row => row.request.CreatedAtUtc)
            .Select(row => ToMine(row.request, row.DisplayName))
            .ToList();

        return Result<IReadOnlyList<MyAccessRequestDto>>.Success(mine);
    }

    public async Task<Result<MyAccessRequestDto>> SubmitAsync(SubmitAccessRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_caller.UserId is not Guid userId)
        {
            return Result<MyAccessRequestDto>.Failure(AccessErrors.SignInRequired);
        }

        var application = await _db.Applications
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == request.ApplicationId, cancellationToken)
            .ConfigureAwait(false);

        // An application that does not take requests is as invisible here as in the catalog.
        if (application is null || application.Status != ApplicationStatus.Active || !application.AllowAccessRequests)
        {
            return Result<MyAccessRequestDto>.Failure(
                application is null || application.Status != ApplicationStatus.Active
                    ? AccessErrors.ApplicationNotFound
                    : AccessErrors.RequestsNotAccepted);
        }

        if (await _access.HasGrantAsync(application.Id, userId, cancellationToken).ConfigureAwait(false))
        {
            return Result<MyAccessRequestDto>.Failure(AccessErrors.AlreadyHasAccess);
        }

        var pending = await _db.AccessRequests
            .AnyAsync(
                candidate => candidate.UserId == userId
                    && candidate.ApplicationId == application.Id
                    && candidate.Status == AccessRequestStatus.Pending,
                cancellationToken)
            .ConfigureAwait(false);

        if (pending)
        {
            return Result<MyAccessRequestDto>.Failure(AccessErrors.RequestAlreadyPending);
        }

        var submitted = AccessRequest.Submit(Guid.NewGuid(), application.Id, userId, request.Reason, _clock.UtcNow);
        if (submitted.IsFailure)
        {
            return Result<MyAccessRequestDto>.Failure(submitted.Error);
        }

        _db.AccessRequests.Add(submitted.Value);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var directory = await _directory.FindAsync([userId], cancellationToken).ConfigureAwait(false);
        var requesterName = directory.TryGetValue(userId, out var requester) ? requester.DisplayName : null;

        await _audit.RecordAsync(
                AuditEvent.Succeeded(
                    AccessAuditActions.RequestSubmitted,
                    application.ToAuditSubject(),
                    AccessAudit.Details(("reason", submitted.Value.Reason))),
                cancellationToken)
            .ConfigureAwait(false);

        await _notifier.NotifyAsync(
                new Notice
                {
                    Type = AccessNotificationTypes.RequestSubmitted,
                    Audience = NoticeAudience.Holders(AccessNotificationRoles.RequestReviewers),
                    Parameters = new Dictionary<string, string?>
                    {
                        ["user"] = requesterName,
                        ["application"] = application.DisplayName,
                        ["reason"] = submitted.Value.Reason,
                    },
                    Link = "/admin/access-requests",
                },
                cancellationToken)
            .ConfigureAwait(false);

        return Result<MyAccessRequestDto>.Success(ToMine(submitted.Value, application.DisplayName));
    }

    public async Task<Result> CancelAsync(Guid requestId, CancellationToken cancellationToken)
    {
        if (_caller.UserId is not Guid userId)
        {
            return Result.Failure(AccessErrors.SignInRequired);
        }

        // Someone else's request is reported as missing, not as forbidden: its existence is none of the caller's business.
        var request = await _db.AccessRequests
            .SingleOrDefaultAsync(candidate => candidate.Id == requestId && candidate.UserId == userId, cancellationToken)
            .ConfigureAwait(false);

        if (request is null)
        {
            return Result.Failure(AccessErrors.RequestNotFound);
        }

        var cancelled = request.Cancel(_clock.UtcNow);
        if (cancelled.IsFailure)
        {
            return cancelled;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var application = await _db.Applications.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == request.ApplicationId, cancellationToken)
            .ConfigureAwait(false);

        await _audit.RecordAsync(AuditEvent.Succeeded(AccessAuditActions.RequestCancelled, application.ToAuditSubject()), cancellationToken)
            .ConfigureAwait(false);

        return Result.Success();
    }

    // -----------------------------------------------------------------------
    // The reviewers' side.
    // -----------------------------------------------------------------------

    public async Task<Result<AccessRequestPageDto>> ListAsync(AccessRequestQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var guard = await _authorization.EnsureCanAsync(AccessOperation.ReviewAccessRequests, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<AccessRequestPageDto>.Failure(guard.Error);
        }

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, AccessRequestQuery.MaxPageSize);

        var requests = _db.AccessRequests.AsNoTracking();

        if (query.ApplicationId is Guid applicationId)
        {
            requests = requests.Where(request => request.ApplicationId == applicationId);
        }

        if (ParseStatus(query.Status) is { } status)
        {
            requests = requests.Where(request => request.Status == status);
        }

        var totalCount = await requests.CountAsync(cancellationToken).ConfigureAwait(false);
        var pendingCount = await _db.AccessRequests
            .CountAsync(request => request.Status == AccessRequestStatus.Pending, cancellationToken)
            .ConfigureAwait(false);

        // The waiting ones alone are a queue, the longest-waiting at the top; a mixed list shows them first and
        // everything else newest first.
        var ordered = ParseStatus(query.Status) == AccessRequestStatus.Pending
            ? requests.OrderBy(request => request.CreatedAtUtc)
            : requests
                .OrderBy(request => request.Status == AccessRequestStatus.Pending ? 0 : 1)
                .ThenByDescending(request => request.CreatedAtUtc);

        var items = await ordered
            .ThenBy(request => request.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var dtos = await ToDtosAsync(items, cancellationToken).ConfigureAwait(false);

        return Result<AccessRequestPageDto>.Success(new AccessRequestPageDto(dtos, page, pageSize, totalCount, pendingCount));
    }

    public async Task<Result<AccessRequestDto>> ApproveAsync(
        Guid requestId,
        ReviewAccessRequest review,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(review);

        var loaded = await LoadForReviewAsync(requestId, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return Result<AccessRequestDto>.Failure(loaded.Error);
        }

        var (request, application, reviewerId) = loaded.Value;

        // The note is checked before anything is granted, so a too-long note cannot leave a grant behind.
        var note = AccessRequest.ValidateNote(review.Note);
        if (note.IsFailure)
        {
            return Result<AccessRequestDto>.Failure(note.Error);
        }

        // The grant first: the request is only closed once the user can actually open the application.
        var roles = UserGrantOperations.NormaliseRoles(review.Roles) ?? [];
        var granted = await _grants.GrantAsync(application, request.UserId, roles, notifyUser: false, cancellationToken).ConfigureAwait(false);
        if (granted.IsFailure)
        {
            return Result<AccessRequestDto>.Failure(granted.Error);
        }

        var approved = request.Approve(reviewerId, roles, review.Note, _clock.UtcNow);
        if (approved.IsFailure)
        {
            return Result<AccessRequestDto>.Failure(approved.Error);
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await RecordDecisionAsync(AccessAuditActions.RequestApproved, AccessNotificationTypes.RequestApproved, request, application, cancellationToken)
            .ConfigureAwait(false);

        return Result<AccessRequestDto>.Success((await ToDtosAsync([request], cancellationToken).ConfigureAwait(false))[0]);
    }

    public async Task<Result<AccessRequestDto>> RejectAsync(
        Guid requestId,
        ReviewAccessRequest review,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(review);

        var loaded = await LoadForReviewAsync(requestId, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return Result<AccessRequestDto>.Failure(loaded.Error);
        }

        var (request, application, reviewerId) = loaded.Value;

        var rejected = request.Reject(reviewerId, review.Note, _clock.UtcNow);
        if (rejected.IsFailure)
        {
            return Result<AccessRequestDto>.Failure(rejected.Error);
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await RecordDecisionAsync(AccessAuditActions.RequestRejected, AccessNotificationTypes.RequestRejected, request, application, cancellationToken)
            .ConfigureAwait(false);

        return Result<AccessRequestDto>.Success((await ToDtosAsync([request], cancellationToken).ConfigureAwait(false))[0]);
    }

    /// <summary>The request to decide, its application and the reviewer, once the reviewer has been allowed.</summary>
    private async Task<Result<(AccessRequest Request, PortalApplication Application, Guid ReviewerId)>> LoadForReviewAsync(
        Guid requestId,
        CancellationToken cancellationToken)
    {
        var guard = await _authorization.EnsureCanAsync(AccessOperation.ReviewAccessRequests, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<(AccessRequest, PortalApplication, Guid)>.Failure(guard.Error);
        }

        if (_caller.UserId is not Guid reviewerId)
        {
            return Result<(AccessRequest, PortalApplication, Guid)>.Failure(AccessErrors.SignInRequired);
        }

        var request = await _db.AccessRequests
            .SingleOrDefaultAsync(candidate => candidate.Id == requestId, cancellationToken)
            .ConfigureAwait(false);

        if (request is null)
        {
            return Result<(AccessRequest, PortalApplication, Guid)>.Failure(AccessErrors.RequestNotFound);
        }

        if (!request.IsPending)
        {
            return Result<(AccessRequest, PortalApplication, Guid)>.Failure(AccessErrors.RequestNotPending);
        }

        var application = await _db.Applications
            .SingleAsync(candidate => candidate.Id == request.ApplicationId, cancellationToken)
            .ConfigureAwait(false);

        return Result<(AccessRequest, PortalApplication, Guid)>.Success((request, application, reviewerId));
    }

    private async Task RecordDecisionAsync(
        string auditAction,
        string notificationType,
        AccessRequest request,
        PortalApplication application,
        CancellationToken cancellationToken)
    {
        await _audit.RecordAsync(
                AuditEvent.Succeeded(
                    auditAction,
                    await _directory.UserSubjectAsync(request.UserId, cancellationToken).ConfigureAwait(false),
                    AccessAudit.Details(
                        ("application", application.DisplayName),
                        ("clientId", application.ClientId),
                        ("roles", request.GrantedRoles.Count > 0 ? AccessAudit.JoinRoles(request.GrantedRoles) : null),
                        ("note", request.DecisionNote))),
                cancellationToken)
            .ConfigureAwait(false);

        await _notifier.NotifyAsync(
                new Notice
                {
                    Type = notificationType,
                    Audience = NoticeAudience.Users(request.UserId),
                    Parameters = new Dictionary<string, string?>
                    {
                        ["application"] = application.DisplayName,
                        ["note"] = request.DecisionNote,
                    },
                    Link = "/",
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<AccessRequestDto>> ToDtosAsync(IReadOnlyList<AccessRequest> requests, CancellationToken cancellationToken)
    {
        if (requests.Count == 0)
        {
            return [];
        }

        var applicationIds = requests.Select(request => request.ApplicationId).Distinct().ToArray();
        var applications = await _db.Applications
            .AsNoTracking()
            .Where(application => applicationIds.Contains(application.Id))
            .ToDictionaryAsync(application => application.Id, cancellationToken)
            .ConfigureAwait(false);

        var resolve = await AccessQueries.ResolveUsersAsync(
                _directory,
                requests.Select(request => request.UserId)
                    .Concat(requests.Where(request => request.DecidedByUserId is not null).Select(request => request.DecidedByUserId!.Value)),
                cancellationToken)
            .ConfigureAwait(false);

        var result = new List<AccessRequestDto>(requests.Count);

        foreach (var request in requests)
        {
            var application = applications[request.ApplicationId];

            result.Add(new AccessRequestDto(
                request.Id,
                application.ToReference(),
                resolve(request.UserId),
                request.Reason,
                ToContract(request.Status),
                request.CreatedAtUtc,
                request.DecidedAtUtc,
                request.DecidedByUserId is Guid reviewer ? resolve(reviewer) : null,
                request.DecisionNote,
                request.GrantedRoles,
                application.RolesToContract(),

                // Only worth the lookups while the decision is still to be made.
                request.IsPending && await _access.HasGrantAsync(application.Id, request.UserId, cancellationToken).ConfigureAwait(false)));
        }

        return result;
    }

    private static MyAccessRequestDto ToMine(AccessRequest request, string applicationName) => new(
        request.Id,
        request.ApplicationId,
        applicationName,
        request.Reason,
        ToContract(request.Status),
        request.CreatedAtUtc,
        request.DecidedAtUtc,
        request.DecisionNote);

    private static string ToContract(AccessRequestStatus status) => status switch
    {
        AccessRequestStatus.Pending => AccessRequestStatuses.Pending,
        AccessRequestStatus.Approved => AccessRequestStatuses.Approved,
        AccessRequestStatus.Rejected => AccessRequestStatuses.Rejected,
        AccessRequestStatus.Cancelled => AccessRequestStatuses.Cancelled,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    private static AccessRequestStatus? ParseStatus(string? status) => status?.Trim().ToLowerInvariant() switch
    {
        AccessRequestStatuses.Pending => AccessRequestStatus.Pending,
        AccessRequestStatuses.Approved => AccessRequestStatus.Approved,
        AccessRequestStatuses.Rejected => AccessRequestStatus.Rejected,
        AccessRequestStatuses.Cancelled => AccessRequestStatus.Cancelled,
        _ => null,
    };
}
