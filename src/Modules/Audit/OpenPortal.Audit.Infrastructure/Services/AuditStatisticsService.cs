using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenPortal.Audit.Application.Abstractions;
using OpenPortal.Audit.Application.Contracts;
using OpenPortal.Audit.Infrastructure.Configuration;
using OpenPortal.Audit.Infrastructure.Persistence;
using OpenPortal.SharedKernel.Auditing;
using OpenPortal.SharedKernel.Results;
using OpenPortal.SharedKernel.Time;

namespace OpenPortal.Audit.Infrastructure.Services;

/// <summary>
/// Sign-in activity read from the audit log: the log already records every sign-in with its outcome, account,
/// application and address, so the statistics need no second store and agree with the log by construction.
/// <para>
/// The rows of the two periods are read once, narrowed to a few columns, and counted in memory: grouping by a day
/// of a time zone is not something SQLite and PostgreSQL translate alike, and a period is at most
/// <see cref="ActivityStatisticsRequest.MaxDays"/> days of sign-ins.
/// </para>
/// </summary>
internal sealed class AuditStatisticsService : IAuditStatisticsService
{
    private const int TopCount = 8;

    private readonly AuditDbContext _db;
    private readonly IAuditLogAuthorization _authorization;
    private readonly IOptions<AuditModuleOptions> _options;
    private readonly IClock _clock;

    public AuditStatisticsService(
        AuditDbContext db,
        IAuditLogAuthorization authorization,
        IOptions<AuditModuleOptions> options,
        IClock clock)
    {
        _db = db;
        _authorization = authorization;
        _options = options;
        _clock = clock;
    }

    public async Task<Result<ActivityStatisticsDto>> GetActivityAsync(
        ActivityStatisticsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var guard = await _authorization.EnsureCanReadStatisticsAsync(cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<ActivityStatisticsDto>.Failure(guard.Error);
        }

        var days = Math.Clamp(request.Days, 1, ActivityStatisticsRequest.MaxDays);
        var zone = FindTimeZone(request.TimeZone);
        var now = _clock.UtcNow;

        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        var firstDay = today.AddDays(1 - days);
        var periodStart = StartOf(firstDay, zone);
        var previousStart = StartOf(firstDay.AddDays(-days), zone);

        var actions = new[] { request.SignInAction, request.ApplicationSignInAction };

        var rows = await _db.Entries
            .AsNoTracking()
            .Where(entry => entry.OccurredAtUtc >= previousStart && actions.Contains(entry.Action))
            .Select(entry => new Row(
                entry.OccurredAtUtc,
                entry.Action,
                entry.Outcome,
                entry.ActorId,
                entry.ActorLabel,
                entry.TargetId,
                entry.TargetLabel,
                entry.IpAddress,
                entry.Outcome == AuditOutcome.Failure ? entry.Details : null))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var current = rows.Where(row => row.OccurredAtUtc >= periodStart).ToList();
        var previous = rows.Where(row => row.OccurredAtUtc < periodStart).ToList();

        var portalSignIns = current.Where(row => row.Action == request.SignInAction).ToList();
        var failures = portalSignIns.Where(row => row.Outcome == AuditOutcome.Failure).ToList();
        var applicationSignIns = current
            .Where(row => row.Action == request.ApplicationSignInAction && row.Outcome == AuditOutcome.Success && row.TargetId is not null)
            .ToList();

        var byDay = current.ToLookup(row => DayOf(row.OccurredAtUtc, zone));

        var dayEntries = Enumerable.Range(0, days)
            .Select(offset => firstDay.AddDays(offset))
            .Select(date => Totals(byDay[date].ToList(), request, date))
            .ToList();

        var retention = _options.Value.RetentionDays;

        return Result<ActivityStatisticsDto>.Success(new ActivityStatisticsDto(
            firstDay,
            today,
            zone.Id,
            ToTotals(Totals(current, request, today)),
            ToTotals(Totals(previous, request, today)),
            dayEntries,
            applicationSignIns
                .GroupBy(row => row.TargetId!, StringComparer.Ordinal)
                .Select(group => new ApplicationUsageDto(
                    group.Key,
                    group.MaxBy(row => row.OccurredAtUtc)!.TargetLabel,
                    group.Count(),
                    group.Select(row => row.ActorId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).Count()))
                .OrderByDescending(usage => usage.SignIns)
                .ThenBy(usage => usage.ClientId, StringComparer.Ordinal)
                .Take(TopCount)
                .ToList(),
            Count(failures, row => ReasonOf(row.Details) ?? "unknown"),
            Count(failures, row => row.ActorLabel),
            Count(failures, row => row.IpAddress),
            IsComplete: retention <= 0 || previousStart >= now.AddDays(-retention)));
    }

    private static DailyActivityDto Totals(IReadOnlyCollection<Row> rows, ActivityStatisticsRequest request, DateOnly date)
    {
        var successes = rows.Where(row => row.Outcome == AuditOutcome.Success).ToList();

        return new DailyActivityDto(
            date,
            successes.Count(row => row.Action == request.SignInAction),
            rows.Count(row => row.Action == request.SignInAction && row.Outcome == AuditOutcome.Failure),
            successes.Count(row => row.Action == request.ApplicationSignInAction),
            successes.Select(row => row.ActorId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).Count());
    }

    private static ActivityTotalsDto ToTotals(DailyActivityDto totals) =>
        new(totals.SignIns, totals.FailedSignIns, totals.ApplicationSignIns, totals.ActiveUsers);

    private static List<CountedValueDto> Count(IEnumerable<Row> rows, Func<Row, string?> key) =>
        rows
            .Select(row => (Key: key(row), row.OccurredAtUtc))
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .GroupBy(item => item.Key!, StringComparer.OrdinalIgnoreCase)
            .Select(group => new CountedValueDto(group.Key, group.Count(), group.Max(item => item.OccurredAtUtc)))
            .OrderByDescending(value => value.Count)
            .ThenByDescending(value => value.LastAtUtc)
            .Take(TopCount)
            .ToList();

    private static string? ReasonOf(string? details)
    {
        if (string.IsNullOrEmpty(details))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string?>>(details)?.GetValueOrDefault("reason");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static DateOnly DayOf(DateTimeOffset instant, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);

    /// <summary>The instant <paramref name="day"/> begins in <paramref name="zone"/>.</summary>
    private static DateTimeOffset StartOf(DateOnly day, TimeZoneInfo zone)
    {
        var local = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);

        // A day that starts inside a skipped hour (a daylight saving change at midnight) starts an hour later.
        while (zone.IsInvalidTime(local))
        {
            local = local.AddHours(1);
        }

        return new DateTimeOffset(local, zone.GetUtcOffset(local)).ToUniversalTime();
    }

    private static TimeZoneInfo FindTimeZone(string? id) =>
        !string.IsNullOrWhiteSpace(id) && TimeZoneInfo.TryFindSystemTimeZoneById(id.Trim(), out var zone)
            ? zone
            : TimeZoneInfo.Utc;

    private sealed record Row(
        DateTimeOffset OccurredAtUtc,
        string Action,
        AuditOutcome Outcome,
        string? ActorId,
        string? ActorLabel,
        string? TargetId,
        string? TargetLabel,
        string? IpAddress,
        string? Details);
}
