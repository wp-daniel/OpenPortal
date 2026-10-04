using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenPortal.Web.Infrastructure;

namespace OpenPortal.Web.Geo;

/// <summary>
/// Address suggestions for signed-in users filling in their details: the places behind a postal code, and
/// towns matching a name, always within one country (ISO 3166 alpha-2).
/// </summary>
[ApiController]
[Route("api/geo")]
[Authorize]
[Produces("application/json")]
public sealed class GeoController : ControllerBase
{
    private readonly GeoLookup _geo;

    public GeoController(GeoLookup geo)
    {
        _geo = geo;
    }

    /// <summary>The places sharing a postal code, e.g. <c>GET /api/geo/postal-codes/IT/24020</c>.</summary>
    [HttpGet("postal-codes/{country}/{postalCode}")]
    [ProducesResponseType<GeoLookupDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GeoLookupDto>> PostalCodeAsync(
        string country,
        string postalCode,
        CancellationToken cancellationToken)
    {
        var places = await _geo.PostalCodeAsync(country, postalCode, cancellationToken).ConfigureAwait(false);

        return places.IsSuccess ? Ok(places.Value) : ProblemResults.FromResult(HttpContext, places);
    }

    /// <summary>Towns and cities matching a name, e.g. <c>GET /api/geo/cities?country=IT&amp;q=Berg</c>.</summary>
    [HttpGet("cities")]
    [ProducesResponseType<GeoLookupDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GeoLookupDto>> CitiesAsync(
        [FromQuery] string? country,
        [FromQuery] string? q,
        CancellationToken cancellationToken)
    {
        var places = await _geo.CitiesAsync(country ?? string.Empty, q ?? string.Empty, cancellationToken).ConfigureAwait(false);

        return places.IsSuccess ? Ok(places.Value) : ProblemResults.FromResult(HttpContext, places);
    }
}
