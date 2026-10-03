using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenPortal.Web.Persistence;

namespace OpenPortal.Tests.Integration;

/// <summary>
/// How the host behaves when the bootstrap administrator cannot be created.
/// <para>
/// These are the only tests that deliberately fail to boot. The interesting property is not that the
/// configuration is rejected - it is that the rejection is a named, readable configuration error. An
/// unhandled exception here prints a stack trace through the seeder, which reads like a defect in code that
/// is in fact behaving correctly, and it takes the whole application down for what is a typo in a secret.
/// </para>
/// </summary>
public sealed class BootstrapAdminTests
{
    [Fact]
    public void A_bootstrap_password_that_violates_the_policy_fails_with_a_readable_configuration_error()
    {
        using var factory = new WeakBootstrapPasswordFactory();

        // Touching the client forces the host to build, which is when the startup initializer runs.
        var failure = Assert.Throws<BootstrapAdminException>(() => factory.CreateClient());

        // The message has to name the setting, restate the policy in one clause, and point at the usual
        // cause. A message listing every failed rule in turn is what made this undiagnosable.
        failure.Message.ShouldContain("BootstrapAdmin:Password");
        failure.Message.ShouldContain("at least 12 characters");
        failure.Message.ShouldContain("stored whole");
    }

    [Fact]
    public void A_bootstrap_password_left_blank_names_the_missing_setting()
    {
        using var factory = new BlankBootstrapPasswordFactory();

        var failure = Assert.Throws<BootstrapAdminException>(() => factory.CreateClient());

        failure.Message.ShouldContain("BootstrapAdmin:Password");
        failure.Message.ShouldContain("missing or blank");
        failure.Message.ShouldContain("dotnet user-secrets set");
    }

    /// <summary>Bootstrap enabled with a password that cannot satisfy the configured policy.</summary>
    private sealed class WeakBootstrapPasswordFactory : WebApplicationFactory<Program>
    {
        private readonly string _databasePath = Path.Combine(
            Path.GetTempPath(),
            $"openportal-bootstrap-weak-{Guid.NewGuid():N}.db");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Database:Provider", "Sqlite");
            builder.UseSetting($"ConnectionStrings:OpenPortal", $"Data Source={_databasePath}");
            builder.UseSetting("Database:MigrateOnStartup", "true");
            builder.UseSetting("SpaProxy:Enabled", "false");
            builder.UseSetting("Identity:Cookie:RequireSecure", "false");

            builder.UseSetting("BootstrapAdmin:Enabled", "true");
            builder.UseSetting("BootstrapAdmin:Email", "root@example.com");

            // Too short, so Identity rejects it against every rule at once.
            builder.UseSetting("BootstrapAdmin:Password", "abc");
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (!disposing)
            {
                return;
            }

            foreach (var suffix in new[] { string.Empty, "-shm", "-wal" })
            {
                try
                {
                    File.Delete(_databasePath + suffix);
                }
                catch (IOException)
                {
                    // Best effort.
                }
            }
        }
    }

    /// <summary>Bootstrap enabled with the password never supplied, which is what a lost secret looks like.</summary>
    private sealed class BlankBootstrapPasswordFactory : WebApplicationFactory<Program>
    {
        private readonly string _databasePath = Path.Combine(
            Path.GetTempPath(),
            $"openportal-bootstrap-blank-{Guid.NewGuid():N}.db");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Database:Provider", "Sqlite");
            builder.UseSetting($"ConnectionStrings:OpenPortal", $"Data Source={_databasePath}");
            builder.UseSetting("Database:MigrateOnStartup", "true");
            builder.UseSetting("SpaProxy:Enabled", "false");
            builder.UseSetting("Identity:Cookie:RequireSecure", "false");

            builder.UseSetting("BootstrapAdmin:Enabled", "true");
            builder.UseSetting("BootstrapAdmin:Email", "root@example.com");

            // Enabled, email supplied, password absent: the configuration a half-finished setup leaves behind.
            builder.UseSetting("BootstrapAdmin:Password", string.Empty);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (!disposing)
            {
                return;
            }

            foreach (var suffix in new[] { string.Empty, "-shm", "-wal" })
            {
                try
                {
                    File.Delete(_databasePath + suffix);
                }
                catch (IOException)
                {
                    // Best effort.
                }
            }
        }
    }
}
