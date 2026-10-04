using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Access.Application.Contracts;
using OpenPortal.Access.Domain;
using OpenPortal.Access.Domain.Applications;
using OpenPortal.Access.Infrastructure.Configuration;
using OpenPortal.Access.Infrastructure.Persistence;
using OpenPortal.SharedKernel.Results;
using OpenPortal.SharedKernel.Time;

namespace OpenPortal.Access.Infrastructure.Services;

/// <summary>
/// Lets a running application make itself known: a new one is recorded as waiting for approval, a known one
/// refreshes its heartbeat. An announcement never grants anything by itself.
/// </summary>
internal sealed class ApplicationAnnouncementService : IApplicationAnnouncementService
{
    private readonly AccessDbContext _db;
    private readonly IOptions<AccessModuleOptions> _options;
    private readonly IClock _clock;
    private readonly ILogger<ApplicationAnnouncementService> _logger;

    public ApplicationAnnouncementService(
        AccessDbContext db,
        IOptions<AccessModuleOptions> options,
        IClock clock,
        ILogger<ApplicationAnnouncementService> logger)
    {
        _db = db;
        _options = options;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<AnnouncementResultDto>> AnnounceAsync(
        ApplicationManifest manifest,
        string? provisioningKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var expected = _options.Value.ProvisioningKey;
        if (string.IsNullOrWhiteSpace(expected))
        {
            return Result<AnnouncementResultDto>.Failure(AccessErrors.AnnouncementsDisabled);
        }

        if (!KeysMatch(expected, provisioningKey))
        {
            return Result<AnnouncementResultDto>.Failure(AccessErrors.ProvisioningKeyInvalid);
        }

        var clientId = PortalApplication.ValidateClientId(manifest.ClientId);
        if (clientId.IsFailure)
        {
            return Result<AnnouncementResultDto>.Failure(clientId.Error);
        }

        var details = new ApplicationDetails(
            manifest.DisplayName,
            manifest.Description,
            manifest.BaseUrl,
            manifest.RedirectUris ?? [],
            manifest.PostLogoutRedirectUris ?? []);

        var now = _clock.UtcNow;

        var application = await _db.Applications
            .SingleOrDefaultAsync(candidate => candidate.ClientId == clientId.Value, cancellationToken)
            .ConfigureAwait(false);

        if (application is null)
        {
            var created = PortalApplication.CreateAnnounced(Guid.NewGuid(), clientId.Value, details, manifest.Version, now);
            if (created.IsFailure)
            {
                return Result<AnnouncementResultDto>.Failure(created.Error);
            }

            application = created.Value;
            _db.Applications.Add(application);

            _logger.LogInformation("Application {ClientId} announced itself and is waiting for approval.", application.ClientId);
        }
        else
        {
            var recorded = application.RecordAnnouncement(details, manifest.Version, now);
            if (recorded.IsFailure)
            {
                return Result<AnnouncementResultDto>.Failure(recorded.Error);
            }
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<AnnouncementResultDto>.Success(
            new AnnouncementResultDto(application.ClientId, application.Status.ToContract()));
    }

    /// <summary>Constant-time comparison, so response timing reveals nothing about the key.</summary>
    private static bool KeysMatch(string expected, string? supplied)
    {
        if (string.IsNullOrEmpty(supplied))
        {
            return false;
        }

        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));

        return CryptographicOperations.FixedTimeEquals(expectedHash, suppliedHash);
    }
}
