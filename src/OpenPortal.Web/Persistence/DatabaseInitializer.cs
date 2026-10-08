using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenPortal.Access.Infrastructure.Persistence;
using OpenPortal.Audit.Infrastructure.Persistence;
using OpenPortal.Content.Infrastructure.Persistence;
using OpenPortal.Identity.Domain.Users;
using OpenPortal.Identity.Infrastructure.Bootstrap;
using OpenPortal.Identity.Infrastructure.Persistence;
using OpenPortal.Web.Persistence;

namespace OpenPortal.Web.Persistence;

/// <summary>
/// Thrown when the bootstrap administrator cannot be provisioned.
/// <para>
/// Derives from <see cref="InvalidOperationException"/> so existing handlers keep working, and exists so the
/// failure can be told apart from a genuine defect. It propagates on purpose: a host that swallowed it would
/// report only "the server has not been started", which is a worse message than the one it replaced. The
/// diagnosis therefore lives in <see cref="Exception.Message"/> rather than in a catch block somewhere.
/// </para>
/// </summary>
public sealed class BootstrapAdminException : InvalidOperationException
{
    public BootstrapAdminException(string message)
        : base(message)
    {
    }

    public BootstrapAdminException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Bootstrap account settings, read from user secrets or the environment in development.</summary>
public sealed class BootstrapAdminOptions
{
    public const string SectionName = "BootstrapAdmin";

    /// <summary>
    /// When false the bootstrap step is skipped entirely. Defaults to false so a misconfigured or
    /// accidentally-committed configuration cannot quietly create a known-password account in production.
    /// </summary>
    public bool Enabled { get; set; }

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;
}

/// <summary>
/// Brings the database up to date at startup and provisions the first administrator.
/// <para>
/// This exists for development and single-node deployments, where a human running the application is the
/// deployment pipeline. A multi-node rollout should run migrations as a separate ordered step and leave
/// <see cref="DatabaseOptions.MigrateOnStartup"/> off, because several instances migrating concurrently
/// is a race.
/// </para>
/// </summary>
public sealed class DatabaseInitializer
{
    private readonly IdentityDbContext _identityContext;
    private readonly ContentDbContext _contentContext;
    private readonly AccessDbContext _accessContext;
    private readonly AuditDbContext _auditContext;
    private readonly IdentityDataSeeder _roleSeeder;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IOptions<DatabaseOptions> _databaseOptions;
    private readonly IOptions<BootstrapAdminOptions> _bootstrapOptions;
    private readonly IOptions<IdentityOptions> _identityOptions;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        IdentityDbContext identityContext,
        ContentDbContext contentContext,
        AccessDbContext accessContext,
        AuditDbContext auditContext,
        IdentityDataSeeder roleSeeder,
        UserManager<ApplicationUser> userManager,
        IOptions<DatabaseOptions> databaseOptions,
        IOptions<BootstrapAdminOptions> bootstrapOptions,
        IOptions<IdentityOptions> identityOptions,
        ILogger<DatabaseInitializer> logger)
    {
        _identityContext = identityContext;
        _contentContext = contentContext;
        _accessContext = accessContext;
        _auditContext = auditContext;
        _roleSeeder = roleSeeder;
        _userManager = userManager;
        _databaseOptions = databaseOptions;
        _bootstrapOptions = bootstrapOptions;
        _identityOptions = identityOptions;
        _logger = logger;
    }

    public Task InitialiseAsync(CancellationToken cancellationToken) =>
        InitialiseAsync(forceMigrations: false, cancellationToken);

    /// <param name="forceMigrations">Migrate even when <see cref="DatabaseOptions.MigrateOnStartup"/> is off (<c>--migrate</c>).</param>
    /// <param name="cancellationToken">Stops the work.</param>
    public async Task InitialiseAsync(bool forceMigrations, CancellationToken cancellationToken)
    {
        var database = DatabaseProviderSelector.Read(_databaseOptions);

        if (database.MigrateOnStartup || forceMigrations)
        {
            await MigrateAsync(cancellationToken).ConfigureAwait(false);
        }

        // Roles are always provisioned: they are part of the schema's meaning, not sample data, and the
        // seeder is idempotent.
        await _roleSeeder.SeedAsync(cancellationToken).ConfigureAwait(false);

        await EnsureBootstrapAdministratorAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task MigrateAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Applying database migrations.");

        await _identityContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        await _contentContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        await _accessContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        await _auditContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Database migrations applied.");
    }

    private async Task EnsureBootstrapAdministratorAsync(CancellationToken cancellationToken)
    {
        var bootstrap = _bootstrapOptions.Value;

        if (!bootstrap.Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(bootstrap.Email) || string.IsNullOrWhiteSpace(bootstrap.Password))
        {
            throw new BootstrapAdminException(
                $"BootstrapAdmin:Enabled is true but "
                + $"{(string.IsNullOrWhiteSpace(bootstrap.Email) ? "BootstrapAdmin:Email" : "BootstrapAdmin:Password")} "
                + "is missing or blank. Supply it with "
                + $"\"dotnet user-secrets set \\\"{BootstrapAdminOptions.SectionName}:Password\\\" \\\"<value>\\\"\", "
                + $"or set {BootstrapAdminOptions.SectionName}:Enabled to false to skip this step.");
        }

        var email = bootstrap.Email.Trim();
        var existing = await _userManager.FindByEmailAsync(email).ConfigureAwait(false);

        if (existing is not null)
        {
            _logger.LogInformation("Bootstrap administrator {Email} already exists; leaving it untouched.", email);

            return;
        }

        var displayName = string.IsNullOrWhiteSpace(bootstrap.DisplayName) ? "Portal Administrator" : bootstrap.DisplayName.Trim();
        var nameParts = displayName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // The bootstrap setting only carries a display name, so it is split into the first and last name
        // that every account now has; the administrator can refine them from the account page.
        var account = ApplicationUser.Create(
            Guid.NewGuid(),
            email,
            new UserDetails(nameParts[0], nameParts.Length > 1 ? nameParts[1] : "Administrator", null, null, null, null, null, null, null, null),
            DateTimeOffset.UtcNow);
        if (account.IsFailure)
        {
            throw new BootstrapAdminException($"BootstrapAdmin:DisplayName was rejected: {account.Error.Description}");
        }

        var user = account.Value;
        user.EmailConfirmed = true;

        var created = await _userManager.CreateAsync(user, bootstrap.Password).ConfigureAwait(false);
        if (!created.Succeeded)
        {
            // Identity reports every rule that failed, which for a blank or truncated secret is the whole
            // policy at once and reads like noise. The rules are restated compactly instead, because the
            // cause is nearly always a value that did not survive being passed to the shell.
            throw new BootstrapAdminException(
                $"BootstrapAdmin:Password was rejected for {email}. "
                + $"It must satisfy: {DescribePasswordPolicy()}. "
                + "Check that the value was stored whole - an unquoted or truncated shell argument is the usual cause.");
        }

        var assigned = await _userManager.AddToRoleAsync(user, Roles.Administrator).ConfigureAwait(false);
        if (!assigned.Succeeded)
        {
            // Leaving an account with no role behind would produce an administrator that cannot administer.
            await _userManager.DeleteAsync(user).ConfigureAwait(false);

            var reasons = string.Join("; ", assigned.Errors.Select(error => error.Description));
            throw new BootstrapAdminException(
                $"Bootstrap administrator {email} was created but could not be granted the administrator role: "
                + $"{reasons}. The account has been removed again.");
        }

        _logger.LogWarning("Created bootstrap administrator {Email}. Disable BootstrapAdmin:Enabled once signed in.", email);
    }

    /// <summary>
    /// The configured password policy as one readable clause, so a rejected password can be fixed by reading
    /// a single line instead of parsing Identity's per-rule descriptions.
    /// </summary>
    private string DescribePasswordPolicy()
    {
        var password = _identityOptions.Value.Password;
        var rules = new List<string> { $"at least {password.RequiredLength} characters" };

        if (password.RequiredUniqueChars > 1)
        {
            rules.Add($"{password.RequiredUniqueChars} different characters");
        }

        if (password.RequireLowercase)
        {
            rules.Add("a lowercase letter");
        }

        if (password.RequireUppercase)
        {
            rules.Add("an uppercase letter");
        }

        if (password.RequireDigit)
        {
            rules.Add("a digit");
        }

        if (password.RequireNonAlphanumeric)
        {
            rules.Add("a non-alphanumeric character");
        }

        return string.Join(", ", rules);
    }
}