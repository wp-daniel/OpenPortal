using OpenPortal.SharedKernel.Auditing;

namespace OpenPortal.Audit.Domain;

/// <summary>
/// One line of the audit log: an action, its outcome, who did it, to what, and from where.
/// <para>
/// Entries are append-only: nothing in the application edits one, and only the retention sweep deletes
/// them. Every text is a snapshot taken when the event happened (an email, an application name), so the log
/// stays readable after the subject is renamed or deleted.
/// </para>
/// <para>
/// Recording must not fail because a value is long: an over-long label or user agent is clipped rather than
/// rejected, since refusing to record a security event is worse than recording a shortened one.
/// </para>
/// </summary>
public sealed class AuditEntry
{
    public const int ActionMaxLength = 64;
    public const int SubjectTypeMaxLength = 32;
    public const int SubjectIdMaxLength = 128;
    public const int LabelMaxLength = 256;
    public const int DetailsMaxLength = 4000;
    public const int IpAddressMaxLength = 64;
    public const int UserAgentMaxLength = 512;
    public const int CorrelationIdMaxLength = 128;

    // Required by EF Core.
    private AuditEntry()
    {
    }

    public Guid Id { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    /// <summary>Dotted action code, e.g. <c>user.created</c>.</summary>
    public string Action { get; private set; } = string.Empty;

    public AuditOutcome Outcome { get; private set; }

    public string? ActorType { get; private set; }

    public string? ActorId { get; private set; }

    public string? ActorLabel { get; private set; }

    public string? TargetType { get; private set; }

    public string? TargetId { get; private set; }

    public string? TargetLabel { get; private set; }

    /// <summary>The event's extra facts as a JSON object of strings, or null.</summary>
    public string? Details { get; private set; }

    public string? IpAddress { get; private set; }

    public string? UserAgent { get; private set; }

    /// <summary>The request's trace identifier, the same one an error response reports as <c>traceId</c>.</summary>
    public string? CorrelationId { get; private set; }

    /// <summary>Builds an entry from an event, clipping every text to its column.</summary>
    /// <param name="details">The event's details already serialised as JSON.</param>
    public static AuditEntry Record(
        Guid id,
        DateTimeOffset occurredAtUtc,
        string action,
        AuditOutcome outcome,
        AuditSubject? actor,
        AuditSubject? target,
        string? details,
        AuditOrigin origin)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(Guid.Empty, id);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentNullException.ThrowIfNull(origin);

        return new AuditEntry
        {
            Id = id,
            OccurredAtUtc = occurredAtUtc,
            Action = Clip(action.Trim(), ActionMaxLength)!,
            Outcome = outcome,
            ActorType = Clip(actor?.Type, SubjectTypeMaxLength),
            ActorId = Clip(actor?.Id, SubjectIdMaxLength),
            ActorLabel = Clip(actor?.Label, LabelMaxLength),
            TargetType = Clip(target?.Type, SubjectTypeMaxLength),
            TargetId = Clip(target?.Id, SubjectIdMaxLength),
            TargetLabel = Clip(target?.Label, LabelMaxLength),

            // Details are JSON, so clipping them would leave an unreadable document. Too large a payload is
            // replaced by a marker instead; the modules keep their details to a few short facts anyway.
            Details = details is { Length: > DetailsMaxLength } ? """{"truncated":"true"}""" : details,
            IpAddress = Clip(origin.IpAddress, IpAddressMaxLength),
            UserAgent = Clip(origin.UserAgent, UserAgentMaxLength),
            CorrelationId = Clip(origin.CorrelationId, CorrelationIdMaxLength),
        };
    }

    /// <summary>The category of an action: the part before the first dot (<c>user</c> for <c>user.created</c>).</summary>
    public static string CategoryOf(string action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var dot = action.IndexOf('.', StringComparison.Ordinal);

        return dot < 0 ? action : action[..dot];
    }

    private static string? Clip(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}

/// <summary>Where a request came from, as the host saw it.</summary>
/// <param name="IpAddress">The client address (after any trusted reverse proxy).</param>
/// <param name="UserAgent">The browser or client that made the request.</param>
/// <param name="CorrelationId">The request's trace identifier.</param>
public sealed record AuditOrigin(string? IpAddress, string? UserAgent, string? CorrelationId)
{
    /// <summary>No request: a background job or a startup step.</summary>
    public static AuditOrigin None { get; } = new(null, null, null);
}
