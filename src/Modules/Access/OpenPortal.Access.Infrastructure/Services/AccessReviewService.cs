using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Access.Application.Auditing;
using OpenPortal.Access.Application.Contracts;
using OpenPortal.Access.Application.Notifications;
using OpenPortal.Access.Domain;
using OpenPortal.Access.Domain.Applications;
using OpenPortal.Access.Domain.Reviews;
using OpenPortal.Access.Infrastructure.Persistence;
using OpenPortal.SharedKernel.Auditing;
using OpenPortal.SharedKernel.Notifications;
using OpenPortal.SharedKernel.Results;
using OpenPortal.SharedKernel.Time;

namespace OpenPortal.Access.Infrastructure.Services;

/// <inheritdoc />
internal sealed class AccessReviewService : IAccessReviewService
{
    private readonly AccessDbContext _db;
    private readonly IAccessAdminAuthorization _authorization;
    private readonly IAccessCaller _caller;
    private readonly IUserDirectory _directory;
    private readonly UserGrantOperations _grants;
    private readonly AccessQueries _access;
    private readonly GroupGraph _graph;
    private readonly IClock _clock;
    private readonly IAuditTrail _audit;
    private readonly INotifier _notifier;

    public AccessReviewService(
        AccessDbContext db,
        IAccessAdminAuthorization authorization,
        IAccessCaller caller,
        IUserDirectory directory,
        UserGrantOperations grants,
        AccessQueries access,
        GroupGraph graph,
        IClock clock,
        IAuditTrail audit,
        INotifier notifier)
    {
        _db = db;
        _authorization = authorization;
        _caller = caller;
        _directory = directory;
        _grants = grants;
        _access = access;
        _graph = graph;
        _clock = clock;
        _audit = audit;
        _notifier = notifier;
    }

    public async Task<Result<IReadOnlyList<AccessReviewSummaryDto>>> ListAsync(CancellationToken cancellationToken)
    {
        var guard = await GuardAsync(cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<IReadOnlyList<AccessReviewSummaryDto>>.Failure(guard.Error);
        }

        var reviews = await _db.AccessReviews.AsNoTracking().Include(review => review.Items).ToListAsync(cancellationToken).ConfigureAwait(false);
        var names = await ApplicationNamesAsync(reviews.Select(review => review.ApplicationId), cancellationToken).ConfigureAwait(false);

        return Result<IReadOnlyList<AccessReviewSummaryDto>>.Success(reviews
            .OrderBy(review => review.Status)
            .ThenByDescending(review => review.CreatedAtUtc)
            .Select(review => ToSummary(review, names.GetValueOrDefault(review.ApplicationId) ?? "?"))
            .ToList());
    }

    public async Task<Result<AccessReviewDto>> GetAsync(Guid reviewId, CancellationToken cancellationToken)
    {
        var guard = await GuardAsync(cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<AccessReviewDto>.Failure(guard.Error);
        }

        var review = await FindAsync(reviewId, cancellationToken).ConfigureAwait(false);

        return review is null
            ? Result<AccessReviewDto>.Failure(AccessReviewErrors.NotFound)
            : Result<AccessReviewDto>.Success(await ToDtoAsync(review, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<AccessReviewDto>> StartAsync(StartAccessReviewRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var guard = await GuardAsync(cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<AccessReviewDto>.Failure(guard.Error);
        }

        var application = await _db.Applications.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == request.ApplicationId, cancellationToken)
            .ConfigureAwait(false);

        if (application is null)
        {
            return Result<AccessReviewDto>.Failure(AccessErrors.ApplicationNotFound);
        }

        // The snapshot: every grant there is now, users by address and groups by name.
        var userGrants = await _db.UserGrants.AsNoTracking().Where(grant => grant.ApplicationId == application.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var users = await _directory.FindAsync(userGrants.Select(grant => grant.UserId).ToList(), cancellationToken).ConfigureAwait(false);

        var groupGrants = await _db.GroupGrants.AsNoTracking().Where(grant => grant.ApplicationId == application.Id)
            .Join(_db.Groups, grant => grant.GroupId, group => group.Id, (grant, group) => new { grant.GroupId, group.Name, grant.Roles })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var seeds = userGrants
            .Select(grant => new AccessReviewItemSeed(
                AccessReviewSubjectType.User,
                grant.UserId,
                users.TryGetValue(grant.UserId, out var user) ? user.Email : grant.UserId.ToString(),
                grant.Roles))
            .Concat(groupGrants.Select(grant => new AccessReviewItemSeed(AccessReviewSubjectType.Group, grant.GroupId, grant.Name, grant.Roles)))
            .ToList();

        var started = AccessReview.Start(
            Guid.NewGuid(),
            application.Id,
            string.IsNullOrWhiteSpace(request.Name)
                ? $"{application.DisplayName} {_clock.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}"
                : request.Name,
            request.DueAtUtc,
            request.RevokeUndecided,
            _caller.UserId,
            seeds,
            _clock.UtcNow);

        if (started.IsFailure)
        {
            return Result<AccessReviewDto>.Failure(started.Error);
        }

        var review = started.Value;
        _db.AccessReviews.Add(review);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _audit.RecordAsync(
                AuditEvent.Succeeded(
                    AccessAuditActions.ReviewStarted,
                    application.ToAuditSubject(),
                    AccessAudit.Details(("review", review.Name), ("entries", review.Items.Count.ToString(CultureInfo.InvariantCulture)))),
                cancellationToken)
            .ConfigureAwait(false);

        await _notifier.NotifyAsync(
                new Notice
                {
                    Type = AccessNotificationTypes.ReviewStarted,
                    Audience = NoticeAudience.Holders(AccessNotificationRoles.AccessReviewers),
                    Parameters = new Dictionary<string, string?>
                    {
                        ["review"] = review.Name,
                        ["application"] = application.DisplayName,
                        ["count"] = review.Items.Count.ToString(CultureInfo.InvariantCulture),
                    },
                    Link = $"/admin/access-reviews/{review.Id}",
                },
                cancellationToken)
            .ConfigureAwait(false);

        return Result<AccessReviewDto>.Success(await ToDtoAsync(review, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<AccessReviewDto>> DecideAsync(
        Guid reviewId,
        Guid itemId,
        DecideAccessReviewItemRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var guard = await GuardAsync(cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<AccessReviewDto>.Failure(guard.Error);
        }

        var review = await FindAsync(reviewId, cancellationToken).ConfigureAwait(false);
        if (review is null)
        {
            return Result<AccessReviewDto>.Failure(AccessReviewErrors.NotFound);
        }

        var decision = request.Decision?.Trim().ToLowerInvariant() switch
        {
            "keep" => AccessReviewDecision.Keep,
            "revoke" => AccessReviewDecision.Revoke,
            "pending" => AccessReviewDecision.Pending,
            _ => (AccessReviewDecision)(-1),
        };

        var decided = review.Decide(itemId, decision, request.Note, _caller.UserId, _clock.UtcNow);
        if (decided.IsFailure)
        {
            return Result<AccessReviewDto>.Failure(decided.Error);
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<AccessReviewDto>.Failure(AccessReviewErrors.ConcurrentChange);
        }

        return Result<AccessReviewDto>.Success(await ToDtoAsync(review, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<AccessReviewDto>> CompleteAsync(Guid reviewId, CancellationToken cancellationToken)
    {
        var guard = await GuardAsync(cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<AccessReviewDto>.Failure(guard.Error);
        }

        var review = await FindAsync(reviewId, cancellationToken).ConfigureAwait(false);
        if (review is null)
        {
            return Result<AccessReviewDto>.Failure(AccessReviewErrors.NotFound);
        }

        var completed = review.Complete(_caller.UserId, _clock.UtcNow);
        if (completed.IsFailure)
        {
            return Result<AccessReviewDto>.Failure(completed.Error);
        }

        var application = await _db.Applications.SingleOrDefaultAsync(candidate => candidate.Id == review.ApplicationId, cancellationToken)
            .ConfigureAwait(false);
        if (application is null)
        {
            return Result<AccessReviewDto>.Failure(AccessErrors.ApplicationNotFound);
        }

        var userIds = completed.Value.Where(item => item.SubjectType == AccessReviewSubjectType.User).Select(item => item.SubjectId).ToArray();
        var groups = completed.Value.Where(item => item.SubjectType == AccessReviewSubjectType.Group).ToDictionary(item => item.SubjectId);
        var groupIds = groups.Keys.ToArray();
        var revokedUsers = new List<Guid>();
        var revokedGroups = new List<Guid>();

        try
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            // Claim this revision before removing grants. OpenIddict uses this context and transaction as well.
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            var userGrants = await _db.UserGrants
                .Where(grant => grant.ApplicationId == application.Id && userIds.Contains(grant.UserId)
                    && grant.GrantedAtUtc <= review.CreatedAtUtc)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            var groupGrants = await _db.GroupGrants
                .Where(grant => grant.ApplicationId == application.Id && groupIds.Contains(grant.GroupId)
                    && grant.GrantedAtUtc <= review.CreatedAtUtc)
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            revokedUsers.AddRange(userGrants.Select(grant => grant.UserId));
            revokedGroups.AddRange(groupGrants.Select(grant => grant.GroupId));
            var affectedUsers = revokedUsers.ToHashSet();
            foreach (var groupId in revokedGroups)
            {
                affectedUsers.UnionWith(await _graph.MembersOfGroupAsync(groupId, cancellationToken).ConfigureAwait(false));
            }

            _db.UserGrants.RemoveRange(userGrants);
            _db.GroupGrants.RemoveRange(groupGrants);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await _access.RevokeIfNoLongerAllowedAsync(application, affectedUsers, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<AccessReviewDto>.Failure(AccessReviewErrors.ConcurrentChange);
        }

        foreach (var userId in revokedUsers)
        {
            await _grants.RecordRevocationAsync(application, userId, cancellationToken).ConfigureAwait(false);
        }

        foreach (var groupId in revokedGroups)
        {
            await _audit.RecordAsync(
                    AuditEvent.Succeeded(
                        AccessAuditActions.GroupRevoked,
                        new AuditSubject(AuditSubjectTypes.Group, groupId.ToString(), groups[groupId].SubjectLabel),
                        AccessAudit.Details(("application", application.DisplayName), ("clientId", application.ClientId), ("reason", "access_review"))),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await _audit.RecordAsync(
                AuditEvent.Succeeded(
                    AccessAuditActions.ReviewCompleted,
                    application.ToAuditSubject(),
                    AccessAudit.Details(("review", review.Name), ("revoked", (revokedUsers.Count + revokedGroups.Count).ToString(CultureInfo.InvariantCulture)))),
                cancellationToken)
            .ConfigureAwait(false);

        return Result<AccessReviewDto>.Success(await ToDtoAsync(review, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<AccessReviewDto>> CancelAsync(Guid reviewId, CancellationToken cancellationToken)
    {
        var guard = await GuardAsync(cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<AccessReviewDto>.Failure(guard.Error);
        }

        var review = await FindAsync(reviewId, cancellationToken).ConfigureAwait(false);
        if (review is null)
        {
            return Result<AccessReviewDto>.Failure(AccessReviewErrors.NotFound);
        }

        var cancelled = review.Cancel(_caller.UserId, _clock.UtcNow);
        if (cancelled.IsFailure)
        {
            return Result<AccessReviewDto>.Failure(cancelled.Error);
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<AccessReviewDto>.Failure(AccessReviewErrors.ConcurrentChange);
        }

        await _audit.RecordAsync(
                AuditEvent.Succeeded(AccessAuditActions.ReviewCancelled, details: AccessAudit.Details(("review", review.Name))),
                cancellationToken)
            .ConfigureAwait(false);

        return Result<AccessReviewDto>.Success(await ToDtoAsync(review, cancellationToken).ConfigureAwait(false));
    }

    private Task<Result> GuardAsync(CancellationToken cancellationToken) =>
        _authorization.EnsureCanAsync(AccessOperation.ReviewAccess, cancellationToken);

    private Task<AccessReview?> FindAsync(Guid reviewId, CancellationToken cancellationToken) =>
        _db.AccessReviews.Include(review => review.Items).SingleOrDefaultAsync(review => review.Id == reviewId, cancellationToken);

    private async Task<Dictionary<Guid, string>> ApplicationNamesAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var wanted = ids.Distinct().ToList();

        return await _db.Applications.AsNoTracking()
            .Where(application => wanted.Contains(application.Id))
            .ToDictionaryAsync(application => application.Id, application => application.DisplayName, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<AccessReviewDto> ToDtoAsync(AccessReview review, CancellationToken cancellationToken)
    {
        var names = await ApplicationNamesAsync([review.ApplicationId], cancellationToken).ConfigureAwait(false);

        return new AccessReviewDto(
            ToSummary(review, names.GetValueOrDefault(review.ApplicationId) ?? "?"),
            review.Items
                .OrderBy(item => item.SubjectType)
                .ThenBy(item => item.SubjectLabel, StringComparer.OrdinalIgnoreCase)
                .Select(item => new AccessReviewItemDto(
                    item.Id,
                    item.SubjectType == AccessReviewSubjectType.User ? "user" : "group",
                    item.SubjectId,
                    item.SubjectLabel,
                    item.Roles,
                    item.Decision.ToString().ToLowerInvariant(),
                    item.Note,
                    item.DecidedAtUtc))
                .ToList());
    }

    private static AccessReviewSummaryDto ToSummary(AccessReview review, string applicationName) => new(
        review.Id,
        review.Name,
        review.ApplicationId,
        applicationName,
        review.Status.ToString().ToLowerInvariant(),
        review.Items.Count,
        review.Items.Count(item => item.Decision != AccessReviewDecision.Pending),
        review.Items.Count(item => item.Decision == AccessReviewDecision.Revoke),
        review.RevokeUndecided,
        review.CreatedAtUtc,
        review.DueAtUtc,
        review.ClosedAtUtc);
}
