using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Web.Geo;

/// <summary>A place an address can be in: a town or city, its region, and a postal code when known.</summary>
public sealed record GeoPlaceDto(string City, string? Region, string? PostalCode);

/// <param name="Available">
/// False when the provider could not be reached. Suggestions are a convenience, so an outage is reported
/// this way rather than as an error: the form keeps working as plain inputs.
/// </param>
public sealed record GeoLookupDto(IReadOnlyList<GeoPlaceDto> Places, bool Available);

public static class GeoErrors
{
    public static Error InvalidCountry { get; } =
        Error.Validation("geo.invalid_country", "The country must be a two-letter ISO 3166 code.");

    public static Error InvalidPostalCode { get; } =
        Error.Validation("geo.invalid_postal_code", "The postal code is not valid.");

    public static Error InvalidQuery { get; } =
        Error.Validation("geo.invalid_query", "Type between 2 and 80 characters to search.");
}

/// <summary>
/// Address suggestions for the user forms, fetched server-side so the browser never sends what a user types
/// (or their IP address) to a third party, and so answers can be cached and the providers swapped.
/// </summary>
public sealed class GeoLookup
{
    public const string PostalCodeClient = "geo-postal-codes";
    public const string PlacesClient = "geo-places";

    private const int MaxResults = 10;

    /// <summary>Languages Photon can label places in; any other falls back to the local names.</summary>
    private static readonly HashSet<string> PlacesLanguages = new(StringComparer.Ordinal) { "de", "en", "fr" };

    private readonly IHttpClientFactory _clients;
    private readonly IMemoryCache _cache;
    private readonly GeoOptions _options;
    private readonly ILogger<GeoLookup> _logger;

    public GeoLookup(
        IHttpClientFactory clients,
        IMemoryCache cache,
        IOptions<GeoOptions> options,
        ILogger<GeoLookup> logger)
    {
        _clients = clients;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>The places that share a postal code (in Italy one CAP often covers several towns).</summary>
    public async Task<Result<GeoLookupDto>> PostalCodeAsync(string country, string postalCode, CancellationToken cancellationToken)
    {
        if (!IsCountryCode(country))
        {
            return Result<GeoLookupDto>.Failure(GeoErrors.InvalidCountry);
        }

        var code = postalCode?.Trim() ?? string.Empty;
        if (code.Length is < 2 or > 10 || !code.All(c => char.IsAsciiLetterOrDigit(c) || c is ' ' or '-'))
        {
            return Result<GeoLookupDto>.Failure(GeoErrors.InvalidPostalCode);
        }

        var path = $"{country.ToLowerInvariant()}/{Uri.EscapeDataString(code.ToUpperInvariant())}";

        return Result<GeoLookupDto>.Success(
            await CachedAsync($"postal:{path}", PostalCodeClient, path, ReadPostalCode, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Towns and cities in <paramref name="country"/> whose name matches what was typed.</summary>
    public async Task<Result<GeoLookupDto>> CitiesAsync(string country, string query, CancellationToken cancellationToken)
    {
        if (!IsCountryCode(country))
        {
            return Result<GeoLookupDto>.Failure(GeoErrors.InvalidCountry);
        }

        var term = query?.Trim() ?? string.Empty;
        if (term.Length is < 2 or > 80)
        {
            return Result<GeoLookupDto>.Failure(GeoErrors.InvalidQuery);
        }

        var language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        var path = $"api/?q={Uri.EscapeDataString(term)}&layer=city&limit={MaxResults}&countrycode={country.ToUpperInvariant()}"
            + (PlacesLanguages.Contains(language) ? $"&lang={language}" : string.Empty);

        return Result<GeoLookupDto>.Success(
            await CachedAsync($"places:{path.ToUpperInvariant()}", PlacesClient, path, ReadPlaces, cancellationToken).ConfigureAwait(false));
    }

    private static bool IsCountryCode(string? country) =>
        country is { Length: 2 } && country.All(char.IsAsciiLetter);

    private async Task<GeoLookupDto> CachedAsync(
        string key,
        string client,
        string path,
        Func<JsonElement, IReadOnlyList<GeoPlaceDto>> read,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return new GeoLookupDto([], Available: false);
        }

        if (_cache.TryGetValue(key, out GeoLookupDto? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_options.Timeout);

            using var response = await _clients.CreateClient(client)
                .GetAsync(new Uri(path, UriKind.Relative), timeout.Token)
                .ConfigureAwait(false);

            // Zippopotam answers an unknown postal code with 404: that is "no places", not an outage.
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return Remember(key, new GeoLookupDto([], Available: true));
            }

            response.EnsureSuccessStatusCode();

            await using var body = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            using var json = await JsonDocument.ParseAsync(body, cancellationToken: timeout.Token).ConfigureAwait(false);

            return Remember(key, new GeoLookupDto(read(json.RootElement), Available: true));
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException
            || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            // Not cached: the next keystroke should try again rather than repeat the outage for hours.
            _logger.LogWarning(exception, "Address lookup via {Client} failed", client);

            return new GeoLookupDto([], Available: false);
        }
    }

    private GeoLookupDto Remember(string key, GeoLookupDto value) =>
        _cache.Set(key, value, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = _options.CacheDuration, Size = 1 });

    /// <summary><c>{ "post code": "24020", "places": [{ "place name": "Gorle", "state": "Lombardia" }] }</c></summary>
    private static IReadOnlyList<GeoPlaceDto> ReadPostalCode(JsonElement root)
    {
        if (!root.TryGetProperty("places", out var places) || places.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var postalCode = Text(root, "post code");

        return Distinct(places.EnumerateArray()
            .Select(place => (City: Text(place, "place name"), Region: Text(place, "state")))
            .Where(place => place.City is not null)
            .Select(place => new GeoPlaceDto(TitleCase(place.City!), place.Region, postalCode)));
    }

    /// <summary>GeoJSON features whose <c>properties</c> carry <c>name</c>, <c>state</c> and sometimes <c>postcode</c>.</summary>
    private static IReadOnlyList<GeoPlaceDto> ReadPlaces(JsonElement root)
    {
        if (!root.TryGetProperty("features", out var features) || features.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return Distinct(features.EnumerateArray()
            .Where(feature => feature.TryGetProperty("properties", out _))
            .Select(feature => feature.GetProperty("properties"))
            .Select(properties => (City: Text(properties, "name"), Region: Text(properties, "state"), PostalCode: Text(properties, "postcode")))
            .Where(place => place.City is not null)
            // A postcode listing several codes ("20121;20122") is not one the form can use.
            .Select(place => new GeoPlaceDto(
                place.City!,
                place.Region,
                place.PostalCode is { } code && !code.Contains(';', StringComparison.Ordinal) ? code : null)));
    }

    private static IReadOnlyList<GeoPlaceDto> Distinct(IEnumerable<GeoPlaceDto> places) =>
        places
            .DistinctBy(place => (place.City.ToUpperInvariant(), place.Region?.ToUpperInvariant()))
            .Take(MaxResults * 3)
            .ToArray();

    private static string? Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!.Trim()
            : null;

    /// <summary>Zippopotam spells some Italian places "Vilminore Di Scalve"; lower-case joining words read better.</summary>
    private static string TitleCase(string name) =>
        string.Join(' ', name.Split(' ').Select((word, index) =>
            index > 0 && word is "Di" or "Del" or "Della" or "Dei" or "De" or "Da" or "In" or "Sul" or "Al" or "La" or "Le"
                ? word.ToLowerInvariant()
                : word));
}
