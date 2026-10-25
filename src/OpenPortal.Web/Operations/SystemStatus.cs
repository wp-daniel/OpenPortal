using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Server;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Notifications.Application.Abstractions;
using OpenPortal.Notifications.Application.Contracts;
using OpenPortal.SharedKernel.Jobs;
using OpenPortal.Web.Email;
using OpenPortal.Web.Persistence;
using OpenPortal.Web.Scale;

namespace OpenPortal.Web.Operations;

/// <summary>What the system page shows: this instance, its checks, its background jobs and what may need attention.</summary>
public sealed record SystemStatusDto(
    string Version,
    string Environment,
    string Runtime,
    string MachineName,
    DateTimeOffset StartedAtUtc,
    string DatabaseProvider,
    string HealthStatus,
    IReadOnlyList<SystemCheckDto> Checks,
    IReadOnlyList<SystemJobDto> Jobs,
    OutboxSummaryDto Outbox,
    SystemFeaturesDto Features,
    IReadOnlyList<SigningKeyDto> SigningKeys);

/// <param name="Status"><c>Healthy</c>, <c>Degraded</c> or <c>Unhealthy</c>.</param>
public sealed record SystemCheckDto(string Name, string Status, string? Description, double DurationMs);

/// <param name="LastSucceeded">Null before the first run on this instance.</param>
public sealed record SystemJobDto(
    string Name,
    double IntervalMinutes,
    DateTimeOffset? LastStartedAtUtc,
    double? LastDurationMs,
    bool? LastSucceeded,
    int? LastResult,
    string? LastError,
    int Runs,
    int Failures);

/// <summary>The optional parts of the deployment and whether they are set up.</summary>
public sealed record SystemFeaturesDto(bool Email, bool Redis, int ExternalProviders, bool Telemetry);

/// <summary>A token signing certificate (keys the portal created itself or was given).</summary>
public sealed record SigningKeyDto(string Thumbprint, DateTimeOffset NotBeforeUtc, DateTimeOffset NotAfterUtc, bool InUse);

/// <summary>Gathers the system page. Administrators only (the controller); reads nothing secret.</summary>
public sealed class SystemStatus
{
    private static readonly DateTimeOffset StartedAt = new(Process.GetCurrentProcess().StartTime.ToUniversalTime(), TimeSpan.Zero);

    private readonly HealthCheckService _health;
    private readonly IJobRegistry _jobs;
    private readonly IOutboxMonitor _outbox;
    private readonly IEmailSender _email;
    private readonly IExternalLoginProviders _providers;
    private readonly IOptions<DatabaseOptions> _database;
    private readonly IOptions<OpenIddictServerOptions> _oidc;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public SystemStatus(
        HealthCheckService health,
        IJobRegistry jobs,
        IOutboxMonitor outbox,
        IEmailSender email,
        IExternalLoginProviders providers,
        IOptions<DatabaseOptions> database,
        IOptions<OpenIddictServerOptions> oidc,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _health = health;
        _jobs = jobs;
        _outbox = outbox;
        _email = email;
        _providers = providers;
        _database = database;
        _oidc = oidc;
        _configuration = configuration;
        _environment = environment;
    }

    public async Task<SystemStatusDto> GetAsync(CancellationToken cancellationToken)
    {
        var report = await _health.CheckHealthAsync(cancellationToken).ConfigureAwait(false);
        var version = typeof(SystemStatus).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? typeof(SystemStatus).Assembly.GetName().Version?.ToString()
            ?? "unknown";

        // The certificate OpenIddict signs with is the valid one that lasts longest (see StoredCertificates).
        var now = DateTime.UtcNow;
        var certificates = _oidc.Value.SigningCredentials
            .Select(credentials => credentials.Key)
            .OfType<X509SecurityKey>()
            .Select(key => key.Certificate)
            .ToList();
        var inUse = certificates
            .Where(certificate => certificate.NotBefore.ToUniversalTime() <= now && certificate.NotAfter.ToUniversalTime() > now)
            .MaxBy(certificate => certificate.NotAfter);

        return new SystemStatusDto(
            version.Split('+')[0],
            _environment.EnvironmentName,
            System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            System.Environment.MachineName,
            StartedAt,
            DatabaseProviderSelector.Read(_database).Provider.ToString(),
            report.Status.ToString(),
            report.Entries
                .Select(entry => new SystemCheckDto(entry.Key, entry.Value.Status.ToString(), entry.Value.Description, entry.Value.Duration.TotalMilliseconds))
                .OrderBy(check => check.Name, StringComparer.Ordinal)
                .ToList(),
            _jobs.Snapshot()
                .Select(job => new SystemJobDto(
                    job.Name,
                    job.Interval.TotalMinutes,
                    job.LastStartedAtUtc,
                    job.LastDuration?.TotalMilliseconds,
                    job.LastSucceeded,
                    job.LastResult,
                    job.LastError,
                    job.Runs,
                    job.Failures))
                .ToList(),
            await _outbox.GetSummaryAsync(cancellationToken).ConfigureAwait(false),
            new SystemFeaturesDto(
                await _email.IsConfiguredAsync(cancellationToken).ConfigureAwait(false),
                !string.IsNullOrWhiteSpace(_configuration[$"{RedisOptions.SectionName}:ConnectionString"]),
                _providers.Current.Count,
                !string.IsNullOrWhiteSpace(_configuration["Observability:OtlpEndpoint"])),
            certificates
                .Select(certificate => new SigningKeyDto(
                    certificate.Thumbprint,
                    new DateTimeOffset(certificate.NotBefore.ToUniversalTime(), TimeSpan.Zero),
                    new DateTimeOffset(certificate.NotAfter.ToUniversalTime(), TimeSpan.Zero),
                    ReferenceEquals(certificate, inUse)))
                .OrderBy(key => key.NotBeforeUtc)
                .ToList());
    }
}
