namespace OpenPortal.Web.Localization;

/// <summary>Bound from the <c>Localization</c> configuration section.</summary>
public sealed class LanguageSettings
{
    public const string SectionName = "Localization";

    /// <summary>Language used when the caller asks for none, or for one that is not offered.</summary>
    public string DefaultLanguage { get; set; } = "en";

    /// <summary>
    /// The languages the deployment offers. <c>Name</c> is the language's own name (Italiano, Français), shown
    /// in the selector without translation so a user can always find their language.
    /// </summary>
    public List<LanguageEntry> Languages { get; set; } = [];
}

public sealed class LanguageEntry
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}
