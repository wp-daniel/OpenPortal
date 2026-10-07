using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace OpenPortal.Tests.Integration;

/// <summary>
/// Boots the real host in memory against a throwaway SQLite file.
/// <para>
/// The alternative - starting the app with <c>dotnet run</c> and probing it with a script - cannot observe an
/// authentication cookie being cleared, cannot distinguish "the filter rejected this" from "the route does
/// not exist", and leaves an orphaned process behind when it fails halfway. A factory gives every test its
/// own host and its own database, and runs them in the same process as the test runner.
/// </para>
/// </summary>
public sealed class OpenPortalFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"openportal-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Development, not a bespoke "Testing" name, so the host's own diagnostics apply: unhandled
        // exceptions are reported in full instead of being flattened to "server.unexpected_error", which is
        // the difference between a two-minute and a two-hour failure to diagnose.
        builder.UseEnvironment("Development");

        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting($"ConnectionStrings:OpenPortal", $"Data Source={_databasePath}");
        builder.UseSetting("Database:MigrateOnStartup", "true");

        // Each test provisions the accounts it needs, so the shared bootstrap administrator is off.
        builder.UseSetting("BootstrapAdmin:Enabled", "false");

        // No Vite dev server and no built SPA in a test run; the API is what is under test.
        builder.UseSetting("SpaProxy:Enabled", "false");

        // HTTP only: TLS is not what these tests are about, and a secure cookie would never be issued.
        builder.UseSetting("IdentityModule:Cookie:RequireSecure", "false");
        builder.UseSetting("Identity:Cookie:RequireSecure", "false");

        // In-memory token keys and a throwaway key ring: nothing a test run creates should outlive it.
        builder.UseSetting("Oidc:UseEphemeralKeys", "true");
        builder.UseSetting("DataProtection:KeysPath", _keysPath);

        builder.UseSetting("Access:ProvisioningKey", ProvisioningKey);
    }

    /// <summary>The provisioning key this host accepts from announcing applications.</summary>
    public const string ProvisioningKey = "test-provisioning-key-0123456789";

    private readonly string _keysPath = Path.Combine(Path.GetTempPath(), $"openportal-keys-{Guid.NewGuid():N}");

    /// <summary>Registers an account and returns it, so tests can sign in as it.</summary>
    public async Task<TestUser> CreateUserAsync(
        string email,
        string password,
        params string[] roles)
    {
        using var scope = Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<
            Microsoft.AspNetCore.Identity.UserManager<OpenPortal.Identity.Domain.Users.ApplicationUser>>();
        var rolesManager = scope.ServiceProvider.GetRequiredService<
            Microsoft.AspNetCore.Identity.RoleManager<Microsoft.AspNetCore.Identity.IdentityRole<Guid>>>();

        // Named after the address ("ada@example.com" is "ada Test") so tests can predict the display name.
        var user = OpenPortal.Identity.Domain.Users.ApplicationUser.Create(
            Guid.NewGuid(),
            email,
            new OpenPortal.Identity.Domain.Users.UserDetails(
                email.Split('@')[0], "Test", null, null, null, null, null, null, null, null),
            DateTimeOffset.UtcNow).Value;

        var created = await users.CreateAsync(user, password);
        created.Succeeded.ShouldBeTrue(
            string.Join("; ", created.Errors.Select(error => $"{error.Code}: {error.Description}")));

        foreach (var role in roles.Length == 0 ? ["User"] : roles)
        {
            if (!await rolesManager.RoleExistsAsync(role))
            {
                var roleResult = await rolesManager.CreateAsync(
                    new Microsoft.AspNetCore.Identity.IdentityRole<Guid>(role));
                roleResult.Succeeded.ShouldBeTrue();
            }

            (await users.AddToRoleAsync(user, role)).Succeeded.ShouldBeTrue();
        }

        return new TestUser(user.Id, email, password, roles.Length == 0 ? ["User"] : roles);
    }

    public async ValueTask InitializeAsync()
    {
        // Touching the client forces the host to build, which runs the startup initializer and therefore
        // applies the migrations. Without this the first request would race the database creation.
        using var probe = CreateClient();
        using var response = await probe.GetAsync("/api/auth/session");
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
    }

    public override ValueTask DisposeAsync()
    {
        // The SQLite file and its write-ahead log are not removed when the connection pool is disposed, so
        // delete them explicitly; a failed test should not leave a database behind for the next run.
        foreach (var suffix in new[] { string.Empty, "-shm", "-wal" })
        {
            try
            {
                File.Delete(_databasePath + suffix);
            }
            catch (IOException)
            {
                // Best effort: a locked file just means the operating system will clean it up later.
            }
        }

        try
        {
            Directory.Delete(_keysPath, recursive: true);
        }
        catch (IOException)
        {
            // Best effort, as above (this includes the folder never having been created).
        }

        return base.DisposeAsync();
    }
}

/// <param name="Id">Account identifier.</param>
/// <param name="Email">Sign-in address.</param>
/// <param name="Password">Password satisfying the configured policy.</param>
/// <param name="Roles">Roles granted to the account.</param>
public sealed record TestUser(Guid Id, string Email, string Password, IReadOnlyList<string> Roles);