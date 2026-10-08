using System.Net.Http.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OpenPortal.Client;

/// <summary>
/// Tells the portal that this application exists and is running: once at start-up, then every
/// <see cref="OpenPortalClientOptions.AnnounceIntervalMinutes"/>. The first announcement of a new client id
/// makes it appear in the portal as "waiting for approval"; later ones are the heartbeat behind "last seen".
/// <para>
/// A portal that is down or refuses the announcement is logged and retried at the next interval; it never
/// stops the application.
/// </para>
/// </summary>
internal sealed partial class OpenPortalAnnouncementService : BackgroundService
{
    public const string HttpClientName = "OpenPortal.Announcement";

    private const string ProvisioningKeyHeader = "X-OpenPortal-Provisioning-Key";

    private readonly IHttpClientFactory _httpClients;
    private readonly IOptions<OpenPortalClientOptions> _options;
    private readonly ILogger<OpenPortalAnnouncementService> _logger;

    public OpenPortalAnnouncementService(
        IHttpClientFactory httpClients,
        IOptions<OpenPortalClientOptions> options,
        ILogger<OpenPortalAnnouncementService> logger)
    {
        _httpClients = httpClients;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = _options.Value;

        if (string.IsNullOrWhiteSpace(options.ProvisioningKey))
        {
            LogAnnouncementsOff(_logger);
            return;
        }

        if (string.IsNullOrWhiteSpace(options.BaseUrl) || string.IsNullOrWhiteSpace(options.Authority))
        {
            LogMissingAddresses(_logger);
            return;
        }

        if (string.IsNullOrWhiteSpace(options.ClientSecret))
        {
            LogNoSecret(_logger, options.ClientId);
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, options.AnnounceIntervalMinutes)));

        do
        {
            await AnnounceAsync(options, stoppingToken).ConfigureAwait(false);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    private async Task AnnounceAsync(OpenPortalClientOptions options, CancellationToken cancellationToken)
    {
        var baseUrl = options.BaseUrl!.TrimEnd('/');

        var manifest = new
        {
            clientId = options.ClientId,
            displayName = string.IsNullOrWhiteSpace(options.DisplayName) ? options.ClientId : options.DisplayName,
            description = options.Description,
            baseUrl,
            redirectUris = new[] { baseUrl + options.CallbackPath },
            postLogoutRedirectUris = new[] { baseUrl + options.SignedOutCallbackPath },
            version = string.IsNullOrWhiteSpace(options.Version) ? OpenPortalClientExtensions.DefaultVersion() : options.Version,

            // Left out when the application declares none, so roles an administrator defined are kept.
            roles = options.Roles.Count == 0
                ? null
                : options.Roles.Select(role => new { key = role.Key, displayName = role.DisplayName, description = role.Description }).ToArray(),
        };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(options.Authority), "api/apps/announce"))
            {
                Content = JsonContent.Create(manifest),
            };
            request.Headers.Add(ProvisioningKeyHeader, options.ProvisioningKey);

            var http = _httpClients.CreateClient(HttpClientName);
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<AnnouncementResult>(cancellationToken).ConfigureAwait(false);
                LogAnnounced(_logger, options.ClientId, result?.Status ?? "unknown");
            }
            else
            {
                LogRefused(_logger, options.ClientId, (int)response.StatusCode);
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            LogUnreachable(_logger, exception, options.Authority);
        }
    }

    private sealed record AnnouncementResult(string ClientId, string Status);

    [LoggerMessage(Level = LogLevel.Information, Message = "OpenPortal: no provisioning key configured, the application will not announce itself.")]
    private static partial void LogAnnouncementsOff(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "OpenPortal: Authority and BaseUrl are both required to announce the application.")]
    private static partial void LogMissingAddresses(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "OpenPortal: no client secret for {ClientId} yet. Approve the application in the portal and configure the secret it issues.")]
    private static partial void LogNoSecret(ILogger logger, string clientId);

    [LoggerMessage(Level = LogLevel.Information, Message = "OpenPortal: announced {ClientId}, status {Status}.")]
    private static partial void LogAnnounced(ILogger logger, string clientId, string status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "OpenPortal: the portal refused the announcement of {ClientId} with HTTP {StatusCode}.")]
    private static partial void LogRefused(ILogger logger, string clientId, int statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "OpenPortal: the portal at {Authority} could not be reached.")]
    private static partial void LogUnreachable(ILogger logger, Exception exception, string authority);
}
