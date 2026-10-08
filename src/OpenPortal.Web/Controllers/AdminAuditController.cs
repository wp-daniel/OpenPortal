using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenPortal.Audit.Application.Abstractions;
using OpenPortal.Audit.Application.Contracts;
using OpenPortal.Web.Authorization;
using OpenPortal.Web.Infrastructure;

namespace OpenPortal.Web.Controllers;

/// <summary>The security audit log, for administrators and holders of the audit page. Read-only.</summary>
[ApiController]
[Route("api/admin/audit")]
[Authorize]
[Produces("application/json")]
public sealed class AdminAuditController : ControllerBase
{
    /// <summary>The most rows one export carries; narrow the filter (dates) for more.</summary>
    public const int MaxExportRows = 10_000;

    private readonly IAuditLogService _audit;

    public AdminAuditController(IAuditLogService audit)
    {
        _audit = audit;
    }

    /// <summary>Lists audit entries, newest first, filtered and paged.</summary>
    [RequirePortalPage(PortalPages.Audit)]
    [HttpGet]
    [ProducesResponseType<AuditLogPageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AuditLogPageDto>> ListAsync([FromQuery] AuditLogQuery query, CancellationToken cancellationToken)
    {
        var page = await _audit.ListAsync(query, cancellationToken).ConfigureAwait(false);

        return page.IsSuccess ? Ok(page.Value) : ProblemResults.FromResult(HttpContext, page);
    }

    /// <summary>
    /// Downloads the entries matching the filter as CSV (UTF-8, newest first, at most
    /// <see cref="MaxExportRows"/> rows), for archiving or a spreadsheet.
    /// </summary>
    [RequirePortalPage(PortalPages.Audit)]
    [HttpGet("export")]
    [Produces("text/csv", "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExportAsync([FromQuery] AuditLogFilter filter, CancellationToken cancellationToken)
    {
        var entries = await _audit.ExportAsync(filter, MaxExportRows, cancellationToken).ConfigureAwait(false);
        if (entries.IsFailure)
        {
            return ProblemResults.FromResult(HttpContext, entries);
        }

        var csv = new StringBuilder();
        csv.AppendLine("occurredAtUtc,action,outcome,actorType,actorId,actor,targetType,targetId,target,details,ipAddress,userAgent,correlationId");

        foreach (var entry in entries.Value)
        {
            csv.AppendJoin(
                    ',',
                    Cell(entry.OccurredAtUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)),
                    Cell(entry.Action),
                    Cell(entry.Outcome),
                    Cell(entry.Actor?.Type),
                    Cell(entry.Actor?.Id),
                    Cell(entry.Actor?.Label),
                    Cell(entry.Target?.Type),
                    Cell(entry.Target?.Id),
                    Cell(entry.Target?.Label),
                    Cell(entry.Details.Count == 0 ? null : JsonSerializer.Serialize(entry.Details)),
                    Cell(entry.IpAddress),
                    Cell(entry.UserAgent),
                    Cell(entry.CorrelationId))
                .AppendLine();
        }

        // The byte-order mark makes spreadsheet applications read the file as UTF-8 rather than ANSI.
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        var fileName = $"openportal-audit-{DateTime.UtcNow.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture)}.csv";

        return File(bytes, "text/csv; charset=utf-8", fileName);
    }

    /// <summary>
    /// One CSV field: quoted when it holds a separator, a quote or a line break, and defused when it starts
    /// like a formula, because labels and user agents come from users and a spreadsheet would run them.
    /// </summary>
    private static string Cell(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            value = "'" + value;
        }

        return value.IndexOfAny([',', '"', '\n', '\r']) >= 0
            ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : value;
    }
}
