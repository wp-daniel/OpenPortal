namespace OpenPortal.SharedKernel.Results;

/// <summary>
/// A failure description returned instead of throwing for expected, business-level outcomes.
/// <para>
/// <see cref="Code"/> is a stable, machine-readable identifier that clients may branch on. It must not
/// be localised and must not change meaning between releases. <see cref="Description"/> is intended for
/// logs and developer tooling; the host layer must not forward it to anonymous callers verbatim.
/// </para>
/// </summary>
public sealed record Error(string Code, string Description, ErrorType Type)
{
    /// <summary>The absence of an error. Never surfaced to callers.</summary>
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);

    public static Error Failure(string code, string description) => new(code, description, ErrorType.Failure);

    public static Error Validation(string code, string description) => new(code, description, ErrorType.Validation);

    public static Error NotFound(string code, string description) => new(code, description, ErrorType.NotFound);

    public static Error Conflict(string code, string description) => new(code, description, ErrorType.Conflict);

    public static Error Unauthorized(string code, string description) => new(code, description, ErrorType.Unauthorized);

    public static Error Forbidden(string code, string description) => new(code, description, ErrorType.Forbidden);

    /// <summary>
    /// True when the error carries no information, which is how a successful result marks its error slot.
    /// </summary>
    public bool IsNone => string.IsNullOrEmpty(Code);

    public override string ToString() => IsNone ? "Error.None" : $"{Code} ({Type}): {Description}";
}