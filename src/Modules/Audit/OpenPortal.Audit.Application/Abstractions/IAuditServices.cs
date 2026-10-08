using OpenPortal.Audit.Application.Contracts;
using OpenPortal.Audit.Domain;
using OpenPortal.SharedKernel.Auditing;
using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Audit.Application.Abstractions;

/// <summary>
/// Reads the audit log. Every method re-checks the caller through <see cref="IAuditLogAuthorization"/>.
/// Writing goes through the shared kernel's <see cref="IAuditTrail"/>, which every module records with.
/// </summary>
public interface IAuditLogService
{
    /// <summary>One page of entries matching the filter, newest first.</summary>
    Task<Result<AuditLogPageDto>> ListAsync(AuditLogQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// Every entry matching the filter, newest first, up to <paramref name="maxRows"/>, for a download.
    /// </summary>
    Task<Result<IReadOnlyList<AuditEntryDto>>> ExportAsync(
        AuditLogFilter filter,
        int maxRows,
        CancellationToken cancellationToken);
}

/// <summary>
/// The current caller and where their request came from. Supplied by the host, which owns the HTTP pipeline,
/// so this module stays independent of it.
/// </summary>
public interface IAuditRequestContext
{
    /// <summary>The signed-in caller, or null for an anonymous request or a background job.</summary>
    AuditSubject? Actor { get; }

    AuditOrigin Origin { get; }
}

/// <summary>Decides whether the caller may read the audit log. Supplied by the host, which owns the pages.</summary>
public interface IAuditLogAuthorization
{
    Task<Result> EnsureCanReadAsync(CancellationToken cancellationToken);
}
