using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using OpenPortal.Identity.Domain.Users;

namespace OpenPortal.Identity.Infrastructure.Bootstrap;

/// <summary>
/// Provisions the roles declared in <see cref="Roles"/>. Idempotent: existing roles are left untouched,
/// so it is safe to run on every startup.
/// </summary>
public sealed class IdentityDataSeeder
{
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly ILogger<IdentityDataSeeder> _logger;

    public IdentityDataSeeder(RoleManager<IdentityRole<Guid>> roleManager, ILogger<IdentityDataSeeder> logger)
    {
        _roleManager = roleManager;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        foreach (var role in Roles.All)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await _roleManager.RoleExistsAsync(role).ConfigureAwait(false))
            {
                continue;
            }

            var result = await _roleManager.CreateAsync(new IdentityRole<Guid>(role)).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                var reasons = string.Join("; ", result.Errors.Select(error => error.Description));
                throw new InvalidOperationException($"Unable to provision the '{role}' role: {reasons}");
            }

            _logger.LogInformation("Provisioned role {Role}.", role);
        }
    }
}