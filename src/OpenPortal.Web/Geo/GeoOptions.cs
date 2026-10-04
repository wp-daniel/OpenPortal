namespace OpenPortal.Web.Geo;

/// <summary>
/// Where address suggestions come from. Both default providers are free and need no key:
/// <see href="https://zippopotam.us">Zippopotam.us</see> resolves a postal code to its places, and
/// <see href="https://photon.komoot.io">Photon</see> (OpenStreetMap data) searches places by name.
/// Either can be pointed at a self-hosted instance.
/// </summary>
public sealed class GeoOptions
{
    public const string SectionName = "Geo";

    /// <summary>Off disables every lookup: the endpoints answer "unavailable" and forms stay plain inputs.</summary>
    public bool Enabled { get; set; } = true;

    public Uri PostalCodeBaseUrl { get; set; } = new("https://api.zippopotam.us/");

    public Uri PlacesBaseUrl { get; set; } = new("https://photon.komoot.io/");

    /// <summary>Postal codes and place names change rarely, and caching keeps us polite to free services.</summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromHours(12);

    /// <summary>A suggestion that arrives late is useless, so a slow provider is given up on quickly.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);
}
