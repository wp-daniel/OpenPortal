using Microsoft.EntityFrameworkCore;
using OpenPortal.Audit.Infrastructure.Persistence;
using OpenPortal.SharedKernel.Privacy;

namespace OpenPortal.Audit.Infrastructure.Services;

/// <summary>
/// The activity section of a personal data export: the audit entries about the person (as actor, as the account an
/// administrator acted for, or as target), newest first, up to <see cref="MaxEntries"/>.
/// </summary>
internal sealed class AuditPersonalData : IPersonalDataSource
{
    public const int MaxEntries = 10_000;

    private readonly AuditDbContext _db;

    public AuditPersonalData(AuditDbContext db)
    {
        _db = db;
    }

    public string Section => "activity";

    public async Task<object> ExportAsync(Guid userId, CancellationToken cancellationToken)
    {
        var id = userId.ToString();

        var entries = await _db.Entries
            .AsNoTracking()
            .Where(entry => entry.ActorId == id || entry.TargetId == id || entry.OnBehalfOfId == id)
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .Take(MaxEntries)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entries
            .Select(entry => new
            {
                entry.OccurredAtUtc,
                entry.Action,
                outcome = entry.Outcome.ToString().ToLowerInvariant(),
                role = entry.ActorId == id ? "actor" : entry.OnBehalfOfId == id ? "acted_for" : "target",
                actor = entry.ActorId == id ? null : entry.ActorLabel,
                target = entry.TargetLabel,
                details = entry.Details,

                // The address and browser are the person's own only when they acted themselves.
                ipAddress = entry.ActorId == id ? entry.IpAddress : null,
                userAgent = entry.ActorId == id ? entry.UserAgent : null,
            })
            .ToList();
    }
}
