using System.Globalization;
using Microsoft.Extensions.Options;
using OpenPortal.Identity.Application.Abstractions;

namespace OpenPortal.Web.Localization;

/// <summary>
/// The single source of truth for which UI languages exist. It feeds request localization, the language
/// selector (<c>GET /api/i18n/languages</c>) and the check applied when a user saves a preference.
/// </summary>
public sealed class LanguageCatalog : ILanguageCatalog
{
    private readonly Dictionary<string, LanguageEntry> _byCode;

    public LanguageCatalog(IOptions<LanguageSettings> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var settings = options.Value;

        var entries = settings.Languages
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Code))
            .Select(entry => new LanguageEntry
            {
                Code = entry.Code.Trim(),
                Name = string.IsNullOrWhiteSpace(entry.Name) ? entry.Code.Trim() : entry.Name.Trim(),
            })
            .ToList();

        if (entries.Count == 0)
        {
            entries.Add(new LanguageEntry { Code = "en", Name = "English" });
        }

        _byCode = new Dictionary<string, LanguageEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            // CultureInfo throws for a code it cannot parse, which fails the host at startup with the
            // offending value rather than at the first request that happens to ask for it.
            _ = CultureInfo.GetCultureInfo(entry.Code);
            _byCode.TryAdd(entry.Code, entry);
        }

        Languages = _byCode.Values.ToArray();

        var requestedDefault = settings.DefaultLanguage?.Trim();
        DefaultLanguage = requestedDefault is { Length: > 0 } && _byCode.TryGetValue(requestedDefault, out var found)
            ? found.Code
            : Languages[0].Code;
    }

    public IReadOnlyList<LanguageEntry> Languages { get; }

    public string DefaultLanguage { get; }

    public bool IsSupported(string code) => _byCode.ContainsKey(code);

    public string Canonicalize(string code) => _byCode.TryGetValue(code, out var entry) ? entry.Code : code;
}
