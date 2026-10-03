namespace OpenPortal.Identity.Application.Abstractions;

/// <summary>
/// The UI languages this deployment offers. Implemented by the host, because the list is deployment
/// configuration rather than a property of the Identity module.
/// </summary>
public interface ILanguageCatalog
{
    /// <summary>Whether <paramref name="code"/> is one of the offered languages (case-insensitive).</summary>
    bool IsSupported(string code);

    /// <summary>The canonical spelling of <paramref name="code"/> as configured, e.g. <c>pt-BR</c>.</summary>
    string Canonicalize(string code);
}
