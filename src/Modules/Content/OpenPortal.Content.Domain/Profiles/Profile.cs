using OpenPortal.SharedKernel.Results;
using OpenPortal.SharedKernel.Text;

namespace OpenPortal.Content.Domain.Profiles;

/// <summary>
/// The single public profile rendered by the portal. Owns its social links so that a link cannot end up
/// pointing at a different profile than the one it was authored for.
/// </summary>
public sealed class Profile
{
    public const int DisplayNameMinLength = 2;
    public const int DisplayNameMaxLength = 120;
    public const int HeadlineMaxLength = 160;
    public const int SummaryMaxLength = 2_000;
    public const int LocationMaxLength = 120;
    public const int EmailMaxLength = 256;
    public const int AvatarUrlMaxLength = 512;

    private readonly List<SocialLink> _socialLinks = [];

    // Required by EF Core; the public constructor is the only way to obtain a valid instance.
    private Profile()
    {
    }

    public Profile(Guid id, string displayName, DateTimeOffset createdAtUtc)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(Guid.Empty, id);

        var validated = ValidateDisplayName(displayName);
        if (validated.IsFailure)
        {
            throw new ArgumentException(validated.Error.Description, nameof(displayName));
        }

        Id = id;
        DisplayName = validated.Value;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>Short positioning statement shown under the display name.</summary>
    public string? Headline { get; private set; }

    /// <summary>Long-form biography. Plain text: the client decides how to render it.</summary>
    public string? Summary { get; private set; }

    public string? Location { get; private set; }

    /// <summary>
    /// Contact address for this profile. This is public content and is distinct from the address the person
    /// signs in with; <c>ApplicationUser.Email</c> is never exposed through this entity.
    /// </summary>
    public string? Email { get; private set; }

    public string? AvatarUrl { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public IReadOnlyList<SocialLink> SocialLinks => _socialLinks;

    /// <summary>
    /// Applies a full field set atomically: nothing is mutated unless every field passes validation, so a
    /// rejected edit cannot leave a profile half-updated.
    /// </summary>
    public Result Update(
        string? displayName,
        string? headline,
        string? summary,
        string? location,
        string? email,
        string? avatarUrl,
        DateTimeOffset now)
    {
        var name = ValidateDisplayName(displayName);
        if (name.IsFailure)
        {
            return Result.Failure(name.Error);
        }

        var trimmedHeadline = TextRules.Normalise(headline);
        if (trimmedHeadline?.Length > HeadlineMaxLength)
        {
            return Result.Failure(ContentErrors.HeadlineTooLong);
        }

        var trimmedSummary = TextRules.Normalise(summary);
        if (trimmedSummary?.Length > SummaryMaxLength)
        {
            return Result.Failure(ContentErrors.SummaryTooLong);
        }

        var trimmedLocation = TextRules.Normalise(location);
        if (trimmedLocation?.Length > LocationMaxLength)
        {
            return Result.Failure(ContentErrors.LocationTooLong);
        }

        var trimmedEmail = TextRules.Normalise(email);
        if (trimmedEmail is not null
            && (trimmedEmail.Length > EmailMaxLength || !TextRules.IsPlausibleEmail(trimmedEmail)))
        {
            return Result.Failure(ContentErrors.EmailInvalid);
        }

        var trimmedAvatar = TextRules.Normalise(avatarUrl);
        if (trimmedAvatar is not null
            && (trimmedAvatar.Length > AvatarUrlMaxLength || !TextRules.IsHttpUrl(trimmedAvatar)))
        {
            return Result.Failure(ContentErrors.UrlInvalid);
        }

        DisplayName = name.Value;
        Headline = trimmedHeadline;
        Summary = trimmedSummary;
        Location = trimmedLocation;
        Email = trimmedEmail;
        AvatarUrl = trimmedAvatar;
        UpdatedAtUtc = now;

        return Result.Success();
    }

    /// <summary>Replaces the whole link set in one step, assigning contiguous positions.</summary>
    public Result ReplaceSocialLinks(IEnumerable<SocialLinkDraft> drafts, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(drafts);

        var replacements = new List<SocialLink>();

        foreach (var draft in drafts)
        {
            var validated = ValidateLink(draft);
            if (validated.IsFailure)
            {
                return Result.Failure(validated.Error);
            }

            replacements.Add(new SocialLink(
                Id,
                validated.Value.Platform,
                validated.Value.Url,
                validated.Value.Label,
                replacements.Count,
                now));
        }

        _socialLinks.Clear();
        _socialLinks.AddRange(replacements);
        UpdatedAtUtc = now;

        return Result.Success();
    }

    /// <summary>Appends one link. Position continues from the current count.</summary>
    public Result AddSocialLink(SocialLinkDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var validated = ValidateLink(draft);
        if (validated.IsFailure)
        {
            return Result.Failure(validated.Error);
        }

        _socialLinks.Add(new SocialLink(
            Id,
            validated.Value.Platform,
            validated.Value.Url,
            validated.Value.Label,
            _socialLinks.Count,
            UpdatedAtUtc));

        return Result.Success();
    }

    /// <summary>
    /// Validates a set of drafts without mutating or requiring an instance.
    /// <para>
    /// Exposed so that a caller can reject a bad edit before opening a database transaction, instead of
    /// discovering the problem half-way through a unit of work.
    /// </para>
    /// </summary>
    public static Result ValidateSocialLinks(IEnumerable<SocialLinkDraft> drafts)
    {
        ArgumentNullException.ThrowIfNull(drafts);

        foreach (var draft in drafts)
        {
            var validated = ValidateLink(draft);
            if (validated.IsFailure)
            {
                return Result.Failure(validated.Error);
            }
        }

        return Result.Success();
    }

    /// <summary>Normalises and validates one draft.</summary>
    public static Result<ValidatedSocialLink> ValidateLink(SocialLinkDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var platform = TextRules.Normalise(draft.Platform);
        if (platform is null)
        {
            return Result<ValidatedSocialLink>.Failure(ContentErrors.SocialLinkPlatformRequired);
        }

        if (platform.Length > SocialLink.PlatformMaxLength)
        {
            return Result<ValidatedSocialLink>.Failure(ContentErrors.SocialLinkPlatformTooLong);
        }

        var url = TextRules.Normalise(draft.Url);
        if (url is null || url.Length > SocialLink.UrlMaxLength || !TextRules.IsHttpUrl(url))
        {
            return Result<ValidatedSocialLink>.Failure(ContentErrors.UrlInvalid);
        }

        var label = TextRules.Normalise(draft.Label);
        if (label?.Length > SocialLink.LabelMaxLength)
        {
            return Result<ValidatedSocialLink>.Failure(ContentErrors.SocialLinkLabelTooLong);
        }

        return Result<ValidatedSocialLink>.Success(new ValidatedSocialLink(platform, url, label));
    }

    /// <summary>
    /// Validates a display name without needing an instance.
    /// <para>
    /// Public because a caller that is about to <em>create</em> a profile must be able to reject a bad name
    /// as a result. The constructor can only throw, which an API cannot turn into a status code.
    /// </para>
    /// </summary>
    public static Result<string> ValidateDisplayName(string? displayName)
    {
        var name = TextRules.Normalise(displayName);

        if (name is null)
        {
            return Result<string>.Failure(ContentErrors.DisplayNameRequired);
        }

        if (name.Length < DisplayNameMinLength)
        {
            return Result<string>.Failure(ContentErrors.DisplayNameTooShort);
        }

        return name.Length > DisplayNameMaxLength
            ? Result<string>.Failure(ContentErrors.DisplayNameTooLong)
            : Result<string>.Success(name);
    }
}

/// <summary>Input for one social link, as supplied by an editor.</summary>
/// <param name="Platform">Display name of the network, for example "GitHub".</param>
/// <param name="Url">Absolute http or https link.</param>
/// <param name="Label">Optional override for the anchor text.</param>
public sealed record SocialLinkDraft(string Platform, string Url, string? Label);

/// <summary>A draft that has passed validation, ready to be turned into a <see cref="SocialLink"/>.</summary>
/// <param name="Platform">Trimmed platform name.</param>
/// <param name="Url">Trimmed absolute http or https URL.</param>
/// <param name="Label">Trimmed label, or <see langword="null"/>.</param>
public readonly record struct ValidatedSocialLink(string Platform, string Url, string? Label);