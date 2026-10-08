namespace OpenPortal.SharedKernel.Auditing;

/// <summary>
/// Records what happened, who did it and to what, for the security audit log.
/// <para>
/// A port in the shared kernel, like <see cref="Time.IClock"/>: every module records its own events at the
/// point where it knows exactly what changed, without depending on the module that stores them. The host
/// supplies the implementation and fills in the caller, their address and the request's correlation id.
/// </para>
/// <para>
/// Implementations never throw (other than for cancellation): the change being recorded has already been
/// committed, and failing the request afterwards would invite a retry of something that succeeded. A write
/// that fails is logged instead. Call it <em>after</em> the change is committed, never inside a transaction,
/// so a rolled-back change is never reported as done.
/// </para>
/// </summary>
public interface IAuditTrail
{
    Task RecordAsync(AuditEvent auditEvent, CancellationToken cancellationToken);
}

/// <summary>Whether the audited operation did what was asked.</summary>
public enum AuditOutcome
{
    Success = 0,

    /// <summary>Refused or failed, for example a wrong password or access that was withdrawn.</summary>
    Failure = 1,
}

/// <summary>Something an event is about, or by: an account, an application, a group, a page.</summary>
/// <param name="Type">One of <see cref="AuditSubjectTypes"/>.</param>
/// <param name="Id">The stable identifier (a Guid, a client id, a page key).</param>
/// <param name="Label">
/// A human-readable snapshot (an email, a name) taken at the time of the event, so the entry stays readable
/// after the subject is renamed or deleted.
/// </param>
public sealed record AuditSubject(string Type, string Id, string? Label);

/// <summary>The kinds of <see cref="AuditSubject"/> the modules record.</summary>
public static class AuditSubjectTypes
{
    public const string User = "user";
    public const string Application = "application";
    public const string Group = "group";
    public const string Page = "page";
    public const string Profile = "profile";
    public const string Project = "project";
}

/// <summary>One audited event.</summary>
public sealed record AuditEvent
{
    /// <summary>
    /// Dotted, stable action code such as <c>user.created</c>. Codes are declared as constants by the module
    /// that records them; the client translates them as <c>audit.action.&lt;code&gt;</c>.
    /// </summary>
    public required string Action { get; init; }

    public AuditOutcome Outcome { get; init; } = AuditOutcome.Success;

    /// <summary>What the event is about, when it is about something other than the actor.</summary>
    public AuditSubject? Target { get; init; }

    /// <summary>
    /// Who acted, when the caller is not yet (or no longer) the signed-in user, as during sign-in. Left null,
    /// the implementation records the current caller.
    /// </summary>
    public AuditSubject? Actor { get; init; }

    /// <summary>A few extra facts (a reason, the roles granted). Never secrets or passwords.</summary>
    public IReadOnlyDictionary<string, string?>? Details { get; init; }

    public static AuditEvent Succeeded(string action, AuditSubject? target = null, IReadOnlyDictionary<string, string?>? details = null) =>
        new() { Action = action, Target = target, Details = details };

    public static AuditEvent Failed(string action, AuditSubject? target = null, IReadOnlyDictionary<string, string?>? details = null) =>
        new() { Action = action, Outcome = AuditOutcome.Failure, Target = target, Details = details };
}
