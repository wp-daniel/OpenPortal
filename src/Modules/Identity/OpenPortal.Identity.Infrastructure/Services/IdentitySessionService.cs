using OpenPortal.SharedKernel.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Application.Auditing;
using OpenPortal.Identity.Application.Contracts;
using OpenPortal.Identity.Domain.Settings;
using OpenPortal.Identity.Domain.Users;
using OpenPortal.Identity.Infrastructure.Configuration;
using OpenPortal.Identity.Infrastructure.Persistence;
using OpenPortal.SharedKernel.Auditing;
using OpenPortal.SharedKernel.Results;
using OpenPortal.SharedKernel.Time;

namespace OpenPortal.Identity.Infrastructure.Services;

/// <summary>Ends sessions for the module's own operations (a password change, "sign out everywhere").</summary>
internal sealed class UserSessions
{
    private readonly IdentityDbContext _db;
    private readonly IClock _clock;

    public UserSessions(IdentityDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    /// <summary>Ends every open session of the account except <paramref name="keep"/>; returns how many ended.</summary>
    public async Task<int> EndAllAsync(Guid userId, string? keep, SessionEndReason reason, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var open = await _db.UserSessions
            .Where(session => session.UserId == userId && session.EndedAtUtc == null && session.Id != keep)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var ended = open.Count(session => session.End(now, reason));
        if (ended > 0)
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return ended;
    }
}

/// <inheritdoc />
internal sealed class IdentitySessionService : ISessionService
{
    /// <summary>How many ended sessions the history shows.</summary>
    private const int RecentCount = 15;

    private readonly IdentityDbContext _db;
    private readonly UserSessions _sessions;
    private readonly UserLookup _userLookup;
    private readonly AdministrationGuard _guard;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditTrail _audit;
    private readonly IClock _clock;

    public IdentitySessionService(
        IdentityDbContext db,
        UserSessions sessions,
        UserLookup userLookup,
        AdministrationGuard guard,
        ICurrentUser currentUser,
        IAuditTrail audit,
        IClock clock)
    {
        _db = db;
        _sessions = sessions;
        _userLookup = userLookup;
        _guard = guard;
        _currentUser = currentUser;
        _audit = audit;
        _clock = clock;
    }

    public async Task<Result<AccountSessionsDto>> ListOwnAsync(CancellationToken cancellationToken)
    {
        var lookup = await _userLookup.FindCurrentAsync(cancellationToken).ConfigureAwait(false);

        return lookup.IsFailure
            ? Result<AccountSessionsDto>.Failure(lookup.Error)
            : Result<AccountSessionsDto>.Success(await ListAsync(lookup.Value.Id, _currentUser.SessionId, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result> RevokeOwnAsync(string sessionId, CancellationToken cancellationToken)
    {
        var lookup = await _userLookup.FindCurrentAsync(cancellationToken).ConfigureAwait(false);
        if (lookup.IsFailure)
        {
            return Result.Failure(lookup.Error);
        }

        if (string.Equals(sessionId, _currentUser.SessionId, StringComparison.Ordinal))
        {
            return Result.Failure(UserErrors.SessionIsCurrent);
        }

        var ended = await EndAsync(lookup.Value.Id, sessionId, cancellationToken).ConfigureAwait(false);
        if (ended.IsFailure)
        {
            return Result.Failure(ended.Error);
        }

        await _audit.RecordAsync(AuditEvent.Succeeded(IdentityAuditActions.SessionRevoked, details: Describe(ended.Value)), cancellationToken)
            .ConfigureAwait(false);

        return Result.Success();
    }

    public async Task<Result<int>> RevokeOtherOwnAsync(CancellationToken cancellationToken)
    {
        var lookup = await _userLookup.FindCurrentAsync(cancellationToken).ConfigureAwait(false);
        if (lookup.IsFailure)
        {
            return Result<int>.Failure(lookup.Error);
        }

        var count = await _sessions.EndAllAsync(lookup.Value.Id, _currentUser.SessionId, SessionEndReason.Revoked, cancellationToken)
            .ConfigureAwait(false);

        if (count > 0)
        {
            await _audit.RecordAsync(
                    AuditEvent.Succeeded(
                        IdentityAuditActions.OtherSessionsRevoked,
                        details: new Dictionary<string, string?> { ["count"] = count.ToString(System.Globalization.CultureInfo.InvariantCulture) }),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return Result<int>.Success(count);
    }

    public async Task<Result<AccountSessionsDto>> ListForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var guard = await _guard.EnsureCanAsync(UserAdministrationOperation.ManageUsers, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<AccountSessionsDto>.Failure(guard.Error);
        }

        var lookup = await _userLookup.FindAsync(userId, cancellationToken).ConfigureAwait(false);

        return lookup.IsFailure
            ? Result<AccountSessionsDto>.Failure(lookup.Error)
            : Result<AccountSessionsDto>.Success(await ListAsync(userId, _currentUser.SessionId, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result> RevokeForUserAsync(Guid userId, string sessionId, CancellationToken cancellationToken)
    {
        var guard = await _guard.EnsureCanAsync(UserAdministrationOperation.ManageUsers, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return guard;
        }

        var lookup = await _userLookup.FindAsync(userId, cancellationToken).ConfigureAwait(false);
        if (lookup.IsFailure)
        {
            return Result.Failure(lookup.Error);
        }

        var changeable = await _guard.EnsureMayChangeAsync(lookup.Value).ConfigureAwait(false);
        if (changeable.IsFailure)
        {
            return changeable;
        }

        var ended = await EndAsync(userId, sessionId, cancellationToken).ConfigureAwait(false);
        if (ended.IsFailure)
        {
            return Result.Failure(ended.Error);
        }

        await _audit.RecordAsync(
                AuditEvent.Succeeded(IdentityAuditActions.UserSessionRevoked, lookup.Value.ToAuditSubject(), Describe(ended.Value)),
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success();
    }

    private async Task<Result<UserSession>> EndAsync(Guid userId, string sessionId, CancellationToken cancellationToken)
    {
        var session = await _db.UserSessions
            .FirstOrDefaultAsync(row => row.Id == sessionId && row.UserId == userId, cancellationToken)
            .ConfigureAwait(false);

        if (session is null || !session.IsActiveAt(_clock.UtcNow) || !session.End(_clock.UtcNow, SessionEndReason.Revoked))
        {
            return Result<UserSession>.Failure(UserErrors.SessionNotFound);
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<UserSession>.Success(session);
    }

    private async Task<AccountSessionsDto> ListAsync(Guid userId, string? current, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var rows = await _db.UserSessions
            .AsNoTracking()
            .Where(session => session.UserId == userId && session.EndReason != SessionEndReason.Replaced)
            .OrderByDescending(session => session.CreatedAtUtc)
            .Take(200)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        SessionInfoDto ToDto(UserSession session) => new(
            session.Id,
            session.CreatedAtUtc,
            session.LastSeenAtUtc,
            session.ExpiresAtUtc,
            session.EndedAtUtc,
            SessionEndReasons.ToContract(session, now),
            session.IpAddress,
            session.Device,
            (session.Methods ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(method => ExternalLoginProviders.IsKnown(method) ? AuthenticationMethods.Federated : method)
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            string.Equals(session.Id, current, StringComparison.Ordinal),
            session.ImpersonatorId is not null,
            session.Persistent);

        var active = rows.Where(session => session.IsActiveAt(now))
            .OrderByDescending(session => string.Equals(session.Id, current, StringComparison.Ordinal))
            .ThenByDescending(session => session.LastSeenAtUtc)
            .Select(ToDto)
            .ToArray();

        var recent = rows.Where(session => !session.IsActiveAt(now)).Take(RecentCount).Select(ToDto).ToArray();

        return new AccountSessionsDto(active, recent);
    }

    private static Dictionary<string, string?> Describe(UserSession session) => new()
    {
        ["device"] = session.Device,
        ["ip"] = session.IpAddress,
    };
}

/// <summary>Deletes sessions that ended (or expired) longer ago than the history keeps, once an hour.</summary>
internal sealed partial class SessionRetentionService : BackgroundService
{
    /// <summary>The job's name in the job list (Administration → System).</summary>
    public const string JobName = "identity.session_retention";

    private readonly IJobRegistry _jobs;

    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopes;
    private readonly IClock _clock;
    private readonly IOptions<SessionStoreOptions> _options;
    private readonly ILogger<SessionRetentionService> _logger;

    public SessionRetentionService(
        IServiceScopeFactory scopes,
        IClock clock,
        IOptions<SessionStoreOptions> options,
        ILogger<SessionRetentionService> logger,
        IJobRegistry jobs)
    {
        _jobs = jobs;
        jobs.Register(JobName, Interval, SweepAsync);
        _scopes = scopes;
        _clock = clock;
        _options = options;
        _logger = logger;
    }

    /// <summary>Deletes what is due and returns how many sessions went.</summary>
    public async Task<int> SweepAsync(CancellationToken cancellationToken)
    {
        var cutoff = _clock.UtcNow.AddDays(-Math.Max(1, _options.Value.HistoryDays));

        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        // Several instances may sweep at once: a row deleted by one is simply not found by the others.
        return await db.UserSessions
            .Where(session => (session.EndedAtUtc != null && session.EndedAtUtc < cutoff)
                || (session.ExpiresAtUtc != null && session.ExpiresAtUtc < cutoff))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var timer = new PeriodicTimer(Interval);

            do
            {
                try
                {
                    var deleted = await _jobs.RunAsync(JobName, stoppingToken).ConfigureAwait(false);
                    if (deleted > 0)
                    {
                        LogSwept(deleted);
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    // A database that is briefly unavailable (or not migrated yet) must not stop the host.
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted {Count} old sessions.")]
    private partial void LogSwept(int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The session sweep failed.")]
    private partial void LogSweepFailed(Exception exception);
}
