using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenPortal.Audit.Application.Abstractions;
using OpenPortal.Audit.Domain;
using OpenPortal.Audit.Infrastructure.Persistence;
using OpenPortal.SharedKernel.Auditing;
using OpenPortal.SharedKernel.Time;

namespace OpenPortal.Audit.Infrastructure.Services;

/// <summary>
/// Writes audit events to the log, filling in the caller and the request's origin from the host.
/// <para>
/// It has a context of its own, so recording never flushes or disturbs the changes another module is tracking.
/// A failed write is logged at error level and swallowed: by the time an event is recorded the change it
/// describes is already committed, and turning the response into a 500 would invite a retry of something
/// that succeeded.
/// </para>
/// </summary>
internal sealed partial class AuditTrail : IAuditTrail
{
    private readonly AuditDbContext _db;
    private readonly IAuditRequestContext _request;
    private readonly IClock _clock;
    private readonly ILogger<AuditTrail> _logger;

    public AuditTrail(AuditDbContext db, IAuditRequestContext request, IClock clock, ILogger<AuditTrail> logger)
    {
        _db = db;
        _request = request;
        _clock = clock;
        _logger = logger;
    }

    public async Task RecordAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        try
        {
            var details = auditEvent.Details is { Count: > 0 }
                ? JsonSerializer.Serialize(auditEvent.Details)
                : null;

            var entry = AuditEntry.Record(
                Guid.NewGuid(),
                _clock.UtcNow,
                auditEvent.Action,
                auditEvent.Outcome,
                auditEvent.Actor ?? _request.Actor,
                auditEvent.Target,
                details,
                _request.Origin);

            _db.Entries.Add(entry);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // Detached so a later event in the same request does not save this one again after a failure.
            _db.ChangeTracker.Clear();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031 // Recording is best effort by design; see the class remarks.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            _db.ChangeTracker.Clear();
            LogRecordFailed(exception, auditEvent.Action, auditEvent.Outcome);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not record the audit event {Action} ({Outcome}).")]
    private partial void LogRecordFailed(Exception exception, string action, AuditOutcome outcome);
}
