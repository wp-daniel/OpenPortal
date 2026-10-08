using System.ComponentModel.DataAnnotations;

namespace OpenPortal.Audit.Application.Contracts;

/// <summary>An account, application or other subject named in an audit entry, as it was at the time.</summary>
public sealed record AuditSubjectDto(string Type, string Id, string? Label);

/// <summary>One line of the audit log.</summary>
/// <param name="Outcome"><c>success</c> or <c>failure</c>.</param>
/// <param name="Actor">Who acted; null for an anonymous caller or a background job.</param>
public sealed record AuditEntryDto(
    Guid Id,
    DateTimeOffset OccurredAtUtc,
    string Action,
    string Outcome,
    AuditSubjectDto? Actor,
    AuditSubjectDto? Target,
    IReadOnlyDictionary<string, string?> Details,
    string? IpAddress,
    string? UserAgent,
    string? CorrelationId);

/// <summary>A page of the audit log, newest first. Same shape as the user list's page.</summary>
public sealed record AuditLogPageDto(IReadOnlyList<AuditEntryDto> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;
}

/// <summary>Filters shared by the audit log list and its export.</summary>
public class AuditLogFilter
{
    /// <summary>Free text matched against the actor, the target, the action and the address.</summary>
    [StringLength(256, ErrorMessage = "validation.search.max")]
    public string? Search { get; init; }

    /// <summary>The part of the action before the dot, e.g. <c>user</c> or <c>auth</c>.</summary>
    [StringLength(32, ErrorMessage = "validation.search.max")]
    public string? Category { get; init; }

    /// <summary><c>success</c> or <c>failure</c>; anything else is ignored.</summary>
    [StringLength(16, ErrorMessage = "validation.search.max")]
    public string? Outcome { get; init; }

    /// <summary>Only entries where this id is the actor or the target (a user's whole history).</summary>
    [StringLength(128, ErrorMessage = "validation.search.max")]
    public string? Subject { get; init; }

    /// <summary>Inclusive lower bound.</summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>Exclusive upper bound.</summary>
    public DateTimeOffset? To { get; init; }
}

/// <summary>Filter and paging parameters for the audit log.</summary>
public sealed class AuditLogQuery : AuditLogFilter
{
    public const int MaxPageSize = 100;

    [Range(1, int.MaxValue, ErrorMessage = "validation.page.min")]
    public int Page { get; init; } = 1;

    [Range(1, MaxPageSize, ErrorMessage = "validation.pageSize.range")]
    public int PageSize { get; init; } = 25;
}

/// <summary>Values of <see cref="AuditEntryDto.Outcome"/> and of the outcome filter.</summary>
public static class AuditOutcomeNames
{
    public const string Success = "success";
    public const string Failure = "failure";
}
