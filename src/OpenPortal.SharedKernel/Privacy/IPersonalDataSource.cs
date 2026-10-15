namespace OpenPortal.SharedKernel.Privacy;

/// <summary>
/// What one module holds about one person, for the copy of their data they can download (the right of access and
/// to portability). A port in the shared kernel, like <see cref="Auditing.IAuditTrail"/>: each module describes
/// its own data, and the host gathers every source into one file without the modules knowing about each other.
/// <para>
/// Implementations return plain, JSON-serialisable values (records, lists, strings), never secrets: no password
/// hashes, keys, recovery codes or token hashes. They perform no caller check; the host calls them only for the
/// signed-in account holder, after they confirmed their password.
/// </para>
/// </summary>
public interface IPersonalDataSource
{
    /// <summary>The section's name in the export, e.g. <c>account</c> or <c>access</c>.</summary>
    string Section { get; }

    /// <summary>The person's data in this module.</summary>
    Task<object> ExportAsync(Guid userId, CancellationToken cancellationToken);
}
