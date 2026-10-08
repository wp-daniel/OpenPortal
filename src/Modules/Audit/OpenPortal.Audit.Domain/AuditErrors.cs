using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Audit.Domain;

/// <summary>Stable, machine-readable failure codes for the Audit module.</summary>
public static class AuditErrors
{
    public static Error ReadForbidden { get; } =
        Error.Forbidden("audit.read_forbidden", "Reading the audit log requires an administrator account or the audit page.");
}
