using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Access.Application.Auditing;
using OpenPortal.Access.Infrastructure.Persistence;
using OpenPortal.SharedKernel.Auditing;
using OpenPortal.SharedKernel.Time;

namespace OpenPortal.Access.Infrastructure.Services;

/// <summary>
/// Ends temporary access: once a minute, removes the grants and group memberships whose end date has passed, revokes
/// the tokens of whoever lost an application that way, and records each removal (actor: the system, detail
/// <c>reason=expired</c>), which also tells the applications through their webhooks. Until the sweep runs, an expired
/// grant already grants nothing: every access check ignores it.
/// Several instances may run it at once: a row removed by one is simply gone for the others.
/// </summary>
internal sealed partial class AccessExpiryService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private static readonly AuditSubject Actor = new(AuditSubjectTypes.System, "access-expiry", null);

    private readonly IServiceScopeFactory _scopes;
    private readonly IClock _clock;
    private readonly ILogger<AccessExpiryService> _logger;

    public AccessExpiryService(IServiceScopeFactory scopes, IClock clock, ILogger<AccessExpiryService> logger)
    {
        _scopes = scopes;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Removes what expired now and returns how many grants and memberships went.</summary>
    public async Task<int> SweepAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
        var access = scope.ServiceProvider.GetRequiredService<AccessQueries>();
        var graph = scope.ServiceProvider.GetRequiredService<GroupGraph>();
        var directory = scope.ServiceProvider.GetRequiredService<IUserDirectory>();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditTrail>();
        var now = _clock.UtcNow;
        var removed = 0;

        var userGrants = await db.UserGrants.Where(grant => grant.ExpiresAtUtc != null && grant.ExpiresAtUtc <= now)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        foreach (var grant in userGrants)
        {
            var application = await db.Applications.SingleAsync(candidate => candidate.Id == grant.ApplicationId, cancellationToken).ConfigureAwait(false);

            db.UserGrants.Remove(grant);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await access.RevokeIfNoLongerAllowedAsync(application, [grant.UserId], cancellationToken).ConfigureAwait(false);

            await audit.RecordAsync(
                    new AuditEvent
                    {
                        Action = AccessAuditActions.UserRevoked,
                        Actor = Actor,
                        Target = await directory.UserSubjectAsync(grant.UserId, cancellationToken).ConfigureAwait(false),
                        Details = AccessAudit.Details(("application", application.DisplayName), ("clientId", application.ClientId), ("reason", "expired")),
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            removed++;
        }

        var groupGrants = await db.GroupGrants.Where(grant => grant.ExpiresAtUtc != null && grant.ExpiresAtUtc <= now)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        foreach (var grant in groupGrants)
        {
            var application = await db.Applications.SingleAsync(candidate => candidate.Id == grant.ApplicationId, cancellationToken).ConfigureAwait(false);
            var group = await db.Groups.AsNoTracking().SingleAsync(candidate => candidate.Id == grant.GroupId, cancellationToken).ConfigureAwait(false);
            var members = await graph.MembersOfGroupAsync(grant.GroupId, cancellationToken).ConfigureAwait(false);

            db.GroupGrants.Remove(grant);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await access.RevokeIfNoLongerAllowedAsync(application, members, cancellationToken).ConfigureAwait(false);

            await audit.RecordAsync(
                    new AuditEvent
                    {
                        Action = AccessAuditActions.GroupRevoked,
                        Actor = Actor,
                        Target = new AuditSubject(AuditSubjectTypes.Group, group.Id.ToString(), group.Name),
                        Details = AccessAudit.Details(("application", application.DisplayName), ("clientId", application.ClientId), ("reason", "expired")),
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            removed++;
        }

        var memberships = await db.GroupMembers.Where(member => member.ExpiresAtUtc != null && member.ExpiresAtUtc <= now)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        foreach (var membership in memberships)
        {
            var group = await db.Groups.AsNoTracking().SingleAsync(candidate => candidate.Id == membership.GroupId, cancellationToken).ConfigureAwait(false);
            var groups = await graph.AncestorsAndSelfAsync([membership.GroupId], cancellationToken).ConfigureAwait(false);
            var applications = await db.GroupGrants
                .Where(grant => groups.Contains(grant.GroupId))
                .Select(grant => grant.ApplicationId)
                .Distinct()
                .Join(db.Applications, id => id, application => application.Id, (_, application) => application)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            db.GroupMembers.Remove(membership);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            foreach (var application in applications)
            {
                await access.RevokeIfNoLongerAllowedAsync(application, [membership.UserId], cancellationToken).ConfigureAwait(false);
            }

            await audit.RecordAsync(
                    new AuditEvent
                    {
                        Action = AccessAuditActions.GroupMemberRemoved,
                        Actor = Actor,
                        Target = await directory.UserSubjectAsync(membership.UserId, cancellationToken).ConfigureAwait(false),
                        Details = AccessAudit.Details(("group", group.Name), ("groupId", group.Id.ToString()), ("reason", "expired")),
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            removed++;
        }

        return removed;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken).ConfigureAwait(false);

            using var timer = new PeriodicTimer(Interval);

            do
            {
                try
                {
                    var removed = await SweepAsync(stoppingToken).ConfigureAwait(false);
                    if (removed > 0)
                    {
                        LogExpired(removed);
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    LogSweepFailed(exception);
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Ended {Count} expired grants and memberships.")]
    private partial void LogExpired(int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The access expiry sweep failed; it will run again shortly.")]
    private partial void LogSweepFailed(Exception exception);
}
