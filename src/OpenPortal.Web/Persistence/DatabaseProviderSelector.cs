using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace OpenPortal.Web.Persistence;

/// <summary>
/// The single place in the solution that knows which database engine is in use.
/// <para>
/// Every module registers its context through <see cref="Configure"/>, which takes a provider-agnostic
/// options builder, so no module, domain or application project knows the engine and no query is
/// provider-specific.
/// </para>
/// <para>
/// Each provider has its own migrations, because a migration is written for one engine's column types: SQLite's
/// are in this assembly (<c>Persistence/Migrations</c>), PostgreSQL's in <c>OpenPortal.Migrations.PostgreSql</c>.
/// </para>
/// </summary>
public static class DatabaseProviderSelector
{
    /// <summary>The assembly holding the PostgreSQL migrations of every module.</summary>
    public const string PostgreSqlMigrationsAssembly = "OpenPortal.Migrations.PostgreSql";

    /// <summary>
    /// Applies the provider selected by configuration to a module's context.
    /// </summary>
    /// <exception cref="InvalidOperationException">For a provider this host does not know.</exception>
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
                // No retrying execution strategy: the Access module opens its own transactions (an application row
                // and its OpenIddict client commit together), which a retrying strategy refuses.
                options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(PostgreSqlMigrationsAssembly));
                break;

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