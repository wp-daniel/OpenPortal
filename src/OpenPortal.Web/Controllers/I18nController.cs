using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Net.Http.Headers;
using OpenPortal.Web.Localization;
using OpenPortal.Web.Resources;

namespace OpenPortal.Web.Controllers;

/// <summary>An offered UI language.</summary>
public sealed record LanguageDto(string Code, string Name);

/// <summary>The offered languages and which one applies when nothing else is chosen.</summary>
public sealed record LanguagesDto(string DefaultLanguage, IReadOnlyList<LanguageDto> Languages);

/// <summary>The translated UI strings for one language.</summary>
public sealed record MessagesDto(string Language, IReadOnlyDictionary<string, string> Messages);

/// <summary>
/// What the client needs to localize itself. Anonymous on purpose: the sign-in page is translated before
/// anyone has an account.
/// <para>
/// The client has no translation files of its own. The same <c>.resx</c> resources that translate server
/// errors are served here, so a language lives in exactly one place.
/// </para>
/// </summary>
[ApiController]
[Route("api/i18n")]
[AllowAnonymous]
[Produces("application/json")]
public sealed class I18nController : ControllerBase
{
    private readonly LanguageCatalog _languages;
    private readonly IStringLocalizer<Messages> _localizer;

    public I18nController(LanguageCatalog languages, IStringLocalizer<Messages> localizer)
    {
        _languages = languages;
        _localizer = localizer;
    }

    /// <summary>Lists the languages this deployment offers.</summary>
    [HttpGet("languages")]
    [ProducesResponseType<LanguagesDto>(StatusCodes.Status200OK)]
    public ActionResult<LanguagesDto> GetLanguages() =>
        Ok(new LanguagesDto(
            _languages.DefaultLanguage,
            _languages.Languages.Select(language => new LanguageDto(language.Code, language.Name)).ToArray()));

    /// <summary>
    /// Returns every message for the request culture (<c>?lang=</c>, then <c>Accept-Language</c>). Parent
    /// cultures are merged in, so a regional file only needs the strings that differ.
    /// </summary>
    [HttpGet("messages")]
    [ProducesResponseType<MessagesDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    public IActionResult GetMessages()
    {
        var messages = _localizer
            .GetAllStrings(includeParentCultures: true)
            .ToDictionary(entry => entry.Name, entry => entry.Value, StringComparer.Ordinal);

        var body = new MessagesDto(CultureInfo.CurrentUICulture.Name, messages);

        // A weak validator over the payload lets the browser revalidate cheaply instead of re-downloading
        // ~300 strings on every page load.
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(body)));
        var etag = new EntityTagHeaderValue($"\"{Convert.ToHexString(hash, 0, 8)}\"");

        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Vary = HeaderNames.AcceptLanguage;
        Response.Headers.ContentLanguage = body.Language;

        if (Request.Headers.IfNoneMatch.Contains(etag.ToString()))
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        Response.Headers.ETag = etag.ToString();

        return Ok(body);
    }
}
