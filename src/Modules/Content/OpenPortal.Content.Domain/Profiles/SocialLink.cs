namespace OpenPortal.Content.Domain.Profiles;

/// <summary>
/// A single external link on a profile. The owning profile id is fixed at construction, so a link cannot be
/// re-pointed at a different profile when the set is reordered or replaced.
/// </summary>
public sealed class SocialLink
{
    public const int PlatformMaxLength = 60;
    public const int UrlMaxLength = 512;
    public const int LabelMaxLength = 120;

    // Required by EF Core.
    private SocialLink()
    {
    }

    internal SocialLink(
        Guid profileId,
        string platform,
        string url,
        string? label,
        int position,
        DateTimeOffset createdAtUtc)
    {
        ProfileId = profileId;
        Platform = platform;
        Url = url;
        Label = label;
        Position = position;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid ProfileId { get; private set; }

    public string Platform { get; private set; } = string.Empty;

    public string Url { get; private set; } = string.Empty;

    /// <summary>Optional anchor text; when null the UI shows <see cref="Platform"/>.</summary>
    public string? Label { get; private set; }

    /// <summary>Zero-based display order, kept contiguous by <see cref="Profile"/>.</summary>
    public int Position { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public Profile Profile { get; private set; } = null!;
}