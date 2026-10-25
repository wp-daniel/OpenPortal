using System.Text.Json;
using Microsoft.Extensions.Options;
using OpenPortal.Access.Application.Abstractions;

namespace OpenPortal.Web.Organizations;

/// <summary><c>Organizations</c> configuration: how organization domains are checked.</summary>
public sealed class OrganizationOptions
{
    public const string SectionName = "Organizations";

    /// <summary>
    /// A DNS-over-HTTPS resolver answering the JSON format (<c>?name=…&amp;type=TXT</c>, <c>application/dns-json</c>),
    /// used to read a domain's TXT records. Empty turns the DNS check off: only a portal administrator can then vouch
    /// for a domain.
    /// </summary>
    public string DnsResolver { get; set; } = "https://dns.google/resolve";

    public int DnsTimeoutSeconds { get; set; } = 10;
}

/// <summary>
/// Reads a domain's TXT records through the configured DNS-over-HTTPS resolver (.NET has no TXT lookup of its own). Only
/// the domain name and the record type leave the portal.
/// </summary>
internal sealed partial class DnsOverHttpsDomainVerifier : IDomainOwnershipVerifier
{
    public const string ClientName = "organization-dns";

    private readonly IHttpClientFactory _clients;
    private readonly IOptions<OrganizationOptions> _options;
    private readonly ILogger<DnsOverHttpsDomainVerifier> _logger;

    public DnsOverHttpsDomainVerifier(IHttpClientFactory clients, IOptions<OrganizationOptions> options, ILogger<DnsOverHttpsDomainVerifier> logger)
    {
        _clients = clients;
        _options = options;
        _logger = logger;
    }

    public async Task<bool> HasTxtRecordAsync(string domain, string value, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var resolver = _options.Value.DnsResolver?.Trim();
        if (string.IsNullOrEmpty(resolver))
        {
            return false;
        }

        try
        {
            var client = _clients.CreateClient(ClientName);
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{resolver}{(resolver.Contains('?', StringComparison.Ordinal) ? '&' : '?')}name={Uri.EscapeDataString(domain)}&type=TXT");
            request.Headers.Accept.ParseAdd("application/dns-json");

            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                LogResolverFailed(domain, (int)response.StatusCode);
                return false;
            }

            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken).ConfigureAwait(false);

            return document.RootElement.TryGetProperty("Answer", out var answers)
                && answers.ValueKind == JsonValueKind.Array
                && answers.EnumerateArray().Any(answer =>
                    answer.TryGetProperty("type", out var type) && type.TryGetInt32(out var code) && code == 16
                    && answer.TryGetProperty("data", out var data)
                    && string.Equals(Unquote(data.GetString()), value, StringComparison.Ordinal));
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException)
        {
            LogResolverUnreachable(domain, exception);
            return false;
        }
    }

    /// <summary>A TXT answer's data is quoted, and a long one is split into several quoted strings.</summary>
    internal static string Unquote(string? data)
    {
        if (string.IsNullOrEmpty(data))
        {
            return string.Empty;
        }

        var parts = data.Split("\" \"", StringSplitOptions.None);
        return string.Concat(parts).Trim().Trim('"');
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The DNS resolver answered {Status} for {Domain}.")]
    private partial void LogResolverFailed(string domain, int status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The DNS resolver could not be reached for {Domain}.")]
    private partial void LogResolverUnreachable(string domain, Exception exception);
}

public static class OrganizationSetup
{
    public static IServiceCollection AddOpenPortalOrganizations(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<OrganizationOptions>().Bind(configuration.GetSection(OrganizationOptions.SectionName));
        services.AddHttpClient(DnsOverHttpsDomainVerifier.ClientName, (provider, client) =>
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(provider.GetRequiredService<IOptions<OrganizationOptions>>().Value.DnsTimeoutSeconds, 1, 60)));
        services.AddScoped<IDomainOwnershipVerifier, DnsOverHttpsDomainVerifier>();

        return services;
    }
}
