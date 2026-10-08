using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpenPortal.Audit.Application.Abstractions;
using OpenPortal.Audit.Application.Contracts;
using OpenPortal.Audit.Domain;
using OpenPortal.Audit.Infrastructure.Persistence;
using OpenPortal.SharedKernel.Auditing;
using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Audit.Infrastructure.Services;

/// <summary>Reads the audit log for the administration screen and its export.</summary>
internal sealed class AuditLogService : IAuditLogService
{
    private static readonly IReadOnlyDictionary<string, string?> NoDetails = new Dictionary<string, string?>();

    private readonly AuditDbContext _db;
    private readonly IAuditLogAuthorization _authorization;

    public AuditLogService(AuditDbContext db, IAuditLogAuthorization authorization)
    {
        _db = db;
        _authorization = authorization;
    }

    public async Task<Result<AuditLogPageDto>> ListAsync(AuditLogQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var guard = await _authorization.EnsureCanReadAsync(cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<AuditLogPageDto>.Failure(guard.Error);
        }

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, AuditLogQuery.MaxPageSize);

        var filtered = Filter(query);
        var totalCount = await filtered.CountAsync(cancellationToken).ConfigureAwait(false);

        var entries = await Newest(filtered)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result<AuditLogPageDto>.Success(
            new AuditLogPageDto(entries.Select(ToDto).ToList(), page, pageSize, totalCount));
    }

    public async Task<Result<IReadOnlyList<AuditEntryDto>>> ExportAsync(
        AuditLogFilter filter,
        int maxRows,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxRows);

        var guard = await _authorization.EnsureCanReadAsync(cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<IReadOnlyList<AuditEntryDto>>.Failure(guard.Error);
        }

        var entries = await Newest(Filter(filter))
            .Take(maxRows)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result<IReadOnlyList<AuditEntryDto>>.Success(entries.Select(ToDto).ToList());
    }

    private IQueryable<AuditEntry> Filter(AuditLogFilter filter)
    {
        var entries = _db.Entries.AsNoTracking();

        var search = filter.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            // Upper-cased on both sides rather than LIKE, so matching is case-insensitive on every provider
            // (SQLite's LIKE ignores case for ASCII, PostgreSQL's does not).
            var term = search.ToUpperInvariant();
            entries = entries.Where(entry =>
                entry.Action.ToUpper().Contains(term)
                || (entry.ActorLabel != null && entry.ActorLabel.ToUpper().Contains(term))
                || (entry.TargetLabel != null && entry.TargetLabel.ToUpper().Contains(term))
                || (entry.IpAddress != null && entry.IpAddress.Contains(search)));
        }

        var category = filter.Category?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(category))
        {
            var prefix = category + ".";
            entries = entries.Where(entry => entry.Action.StartsWith(prefix));
        }

        entries = filter.Outcome?.Trim().ToLowerInvariant() switch
        {
            AuditOutcomeNames.Success => entries.Where(entry => entry.Outcome == AuditOutcome.Success),
            AuditOutcomeNames.Failure => entries.Where(entry => entry.Outcome == AuditOutcome.Failure),
            _ => entries,
        };

        var subject = filter.Subject?.Trim();
        if (!string.IsNullOrEmpty(subject))
        {
            entries = entries.Where(entry => entry.ActorId == subject || entry.TargetId == subject);
        }

        if (filter.From is { } from)
        {
            var fromUtc = from.ToUniversalTime();
            entries = entries.Where(entry => entry.OccurredAtUtc >= fromUtc);
        }

        if (filter.To is { } to)
        {
            var toUtc = to.ToUniversalTime();
            entries = entries.Where(entry => entry.OccurredAtUtc < toUtc);
        }

        return entries;
    }

    // The id breaks ties between entries recorded in the same tick, so paging is stable.
    private static IQueryable<AuditEntry> Newest(IQueryable<AuditEntry> entries) =>
        entries.OrderByDescending(entry => entry.OccurredAtUtc).ThenByDescending(entry => entry.Id);

    private static AuditEntryDto ToDto(AuditEntry entry) => new(
        entry.Id,
        entry.OccurredAtUtc,
        entry.Action,
        entry.Outcome == AuditOutcome.Success ? AuditOutcomeNames.Success : AuditOutcomeNames.Failure,
        Subject(entry.ActorType, entry.ActorId, entry.ActorLabel),
        Subject(entry.TargetType, entry.TargetId, entry.TargetLabel),
        ReadDetails(entry.Details),
        entry.IpAddress,
        entry.UserAgent,
        entry.CorrelationId);

    private static AuditSubjectDto? Subject(string? type, string? id, string? label) =>
        type is null && id is null && label is null
            ? null
            : new AuditSubjectDto(type ?? string.Empty, id ?? string.Empty, label);

    private static IReadOnlyDictionary<string, string?> ReadDetails(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return NoDetails;
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string?>>(json) ?? NoDetails;
        }
        catch (JsonException)
        {
            // Written by this module only, so this means a hand-edited row: show it rather than fail the page.
            return new Dictionary<string, string?> { ["raw"] = json };
        }
    }
}
