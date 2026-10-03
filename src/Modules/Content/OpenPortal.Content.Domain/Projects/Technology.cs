namespace OpenPortal.Content.Domain.Projects;

/// <summary>
/// A tool, language or framework associated with projects. Technologies are shared: one row per distinct
/// name, referenced by every project that uses it, which keeps spelling consistent across the portal.
/// </summary>
public sealed class Technology
{
    public const int NameMaxLength = 80;

    // Required by EF Core.
    private Technology()
    {
    }

    public Technology(Guid id, string name, string normalisedName)
    {
        Id = id;
        Name = name;
        NormalisedName = normalisedName;
    }

    public Guid Id { get; private set; }

    /// <summary>Display form, preserving the casing the owner chose (for example ".NET").</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Upper-cased name used for case-insensitive uniqueness across providers.</summary>
    public string NormalisedName { get; private set; } = string.Empty;

    public static string Normalise(string name) => name.Trim().ToUpperInvariant();
}