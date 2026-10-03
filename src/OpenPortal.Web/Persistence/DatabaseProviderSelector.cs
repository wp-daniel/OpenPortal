using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace OpenPortal.Web.Persistence;

/// <summary>
/// The single place in the solution that knows which database engine is in use.
/// <para>
/// Every module registers its context through <see cref="Configure"/>, which takes a provider-agnostic
/// options builder. Adding PostgreSQL means editing this class and adding the package: no module, domain or
/// application project changes, and no query is provider-specific to begin with.
/// </para>
/// </summary>
public static class DatabaseProviderSelector
{
    /// <summary>
    /// Applies the provider selected by configuration to a module's context.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown for a provider that is declared but not yet implemented, so a misconfigured deployment fails
    /// at startup with a clear message instead of at the first query.
    /// </exception>
    public static void Configure(DbContextOptionsBuilder options, DatabaseProvider provider, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        switch (provider)
        {
            case DatabaseProvider.Sqlite:
                options.UseSqlite(connectionString, sqlite => sqlite.MigrationsAssembly(
                    typeof(DatabaseProviderSelector).Assembly.FullName));
                break;

            case DatabaseProvider.PostgreSql:
                // Intentionally not wired up. Enabling it requires the Npgsql package, which is
                // deliberately absent: see DatabaseOptions.PostgreSql.
                throw new InvalidOperationException(
                    "Database:Provider is set to PostgreSql, but the Npgsql provider has not been added. "
                    + "Add the Npgsql.EntityFrameworkCore.PostgreSQL package to OpenPortal.Web and call "
                    + "UseNpgsql here.");

            default:
                throw new InvalidOperationException($"Unsupported database provider '{provider}'.");
        }
    }

    /// <summary>
    /// Reads and validates the options bound from configuration.
    /// </summary>
    /// <exception cref="OptionsValidationException">When the configuration is missing or incomplete.</exception>
    public static DatabaseOptions Read(IOptions<DatabaseOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var value = options.Value;

        if (string.IsNullOrWhiteSpace(value.ConnectionString))
        {
            throw new OptionsValidationException(
                DatabaseOptions.SectionName,
                typeof(DatabaseOptions),
                [$"Database:{nameof(DatabaseOptions.ConnectionString)} must be configured."]);
        }

        return value;
    }
}