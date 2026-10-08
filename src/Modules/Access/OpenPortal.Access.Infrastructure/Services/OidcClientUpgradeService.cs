using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenPortal.Access.Domain.Applications;
using OpenPortal.Access.Infrastructure.Persistence;

namespace OpenPortal.Access.Infrastructure.Services;

/// <summary>
/// At startup, gives every registered client the permissions the current version grants (for example the
/// <c>roles</c> and <c>groups</c> scopes), so an application approved before an upgrade can ask for them
/// without being approved again.
/// <para>
/// Best effort: a database that is not migrated yet, or another instance doing the same work at the same
/// moment, is logged and left for the next start.
/// </para>
/// </summary>
internal sealed partial class OidcClientUpgradeService : IHostedService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<OidcClientUpgradeService> _logger;

    public OidcClientUpgradeService(IServiceScopeFactory scopes, ILogger<OidcClientUpgradeService> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
            var clients = scope.ServiceProvider.GetRequiredService<OidcClientRegistry>();

            var applications = await db.Applications
                .AsNoTracking()
                .Where(application => application.Status != ApplicationStatus.Pending)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var upgraded = 0;
            foreach (var application in applications)
            {
                if (await clients.UpgradePermissionsAsync(application, cancellationToken).ConfigureAwait(false))
                {
                    upgraded++;
                }
            }

            if (upgraded > 0)
            {
                LogUpgraded(upgraded);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogUpgradeFailed(exception);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Information, Message = "Updated the OpenID Connect permissions of {Count} applications.")]
    private partial void LogUpgraded(int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not update the OpenID Connect permissions of the registered applications.")]
    private partial void LogUpgradeFailed(Exception exception);
}
