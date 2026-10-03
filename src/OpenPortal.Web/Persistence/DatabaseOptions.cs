using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace OpenPortal.Web.Persistence;

/// <summary>Database engines this host can run against.</summary>
public enum DatabaseProvider
{
    /// <summary>SQLite. The default: a single file, no external service, and enough for a single-node portal.</summary>
    Sqlite = 0,

    /// <summary>
    /// PostgreSQL. Reserved for when the deployment needs concurrent writers or managed backups.
    /// <para>
    /// The code path exists but the package is not referenced: adding Npgsql here is the whole of the
    /// migration, which is the point of keeping the choice in the composition root.
    /// </para>
    /// </summary>
    PostgreSql = 1,
}

/// <summary>
/// Bound from the <c>Database</c> configuration section.
/// <para>
/// Both the provider and the connection string are validated at startup. Failing on a misconfigured
/// database immediately is far cheaper than failing on the first request that needs it.
/// </para>
/// </summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>Which engine to use. Bound from the configuration file as a name, not as a number.</summary>
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.Sqlite;

    /// <summary>Connection string for the selected provider.</summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "A database connection string is required.")]
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Applies pending migrations at startup. Intended for development and single-node deployments; a
    /// multi-node rollout should migrate as a separate, ordered step instead.
    /// </summary>
    public bool MigrateOnStartup { get; set; }
}