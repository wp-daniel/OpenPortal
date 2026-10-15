using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Notifications.Domain;

/// <summary>How an announcement is shown: its colour, its icon and how prominently.</summary>
public enum AnnouncementSeverity
{
    Info = 0,
    Warning = 1,

    /// <summary>Planned downtime or degraded service; shown first.</summary>
    Maintenance = 2,
}

/// <summary>Who sees an announcement.</summary>
public enum AnnouncementAudience
{
    /// <summary>Everyone, the sign-in page included.</summary>
    Everyone = 0,

    /// <summary>Only people signed in to the portal.</summary>
    SignedIn = 1,
}

/// <summary>An announcement's text in one language.</summary>
public sealed class AnnouncementText
{
    public const int LanguageMaxLength = 10;
    public const int TitleMaxLength = 120;
    public const int BodyMaxLength = 1000;

    // Required by EF Core.
    private AnnouncementText()
    {
    }

    internal AnnouncementText(string language, string title, string? body)
    {
        Language = language;
        Title = title;
        Body = body;
    }

    /// <summary>A language code such as <c>en</c> or <c>pt-br</c>, lower case.</summary>
    public string Language { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    /// <summary>Plain text (no markup), or null for a title-only banner.</summary>
    public string? Body { get; private set; }
}

/// <summary>What an administrator writes: everything an announcement holds but its identity and history.</summary>
public sealed record AnnouncementContent(
    AnnouncementSeverity Severity,
    AnnouncementAudience Audience,
    DateTimeOffset? StartsAtUtc,
    DateTimeOffset? EndsAtUtc,
    bool Dismissible,
    IReadOnlyList<AnnouncementTextContent> Texts);

/// <summary>One language's text, as written.</summary>
public sealed record AnnouncementTextContent(string? Language, string? Title, string? Body);

/// <summary>
/// A banner shown across the portal for a while: news, a warning, or planned maintenance. Unlike a notification it
/// is addressed to everyone (or everyone signed in), it is not stored per person, and it disappears by itself when
/// its window ends.
/// <para>
/// The text is written per language; a reader whose language has none sees the default language's text (the
/// caller supplies which that is), else the first one written.
/// </para>
/// </summary>
public sealed class Announcement
{
    public const int MaxTexts = 20;

    private readonly List<AnnouncementText> _texts = [];

    // Required by EF Core.
    private Announcement()
    {
    }

    public Guid Id { get; private set; }

    public AnnouncementSeverity Severity { get; private set; }

    public AnnouncementAudience Audience { get; private set; }

    /// <summary>When it starts showing; null: as soon as it is saved.</summary>
    public DateTimeOffset? StartsAtUtc { get; private set; }

    /// <summary>When it stops showing; null: until it is deleted.</summary>
    public DateTimeOffset? EndsAtUtc { get; private set; }

    /// <summary>Whether a reader may hide it (in their own browser).</summary>
    public bool Dismissible { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public IReadOnlyList<AnnouncementText> Texts => _texts;

    public static Result<Announcement> Create(Guid id, AnnouncementContent content, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(Guid.Empty, id);

        var texts = Validate(content);
        if (texts.IsFailure)
        {
            return Result<Announcement>.Failure(texts.Error);
        }

        var announcement = new Announcement { Id = id, CreatedAtUtc = now };
        announcement.Apply(content, texts.Value);

        return Result<Announcement>.Success(announcement);
    }

    /// <summary>Replaces the content. Returns whether anything changed.</summary>
    public Result<bool> Update(AnnouncementContent content, DateTimeOffset now)
    {
        var texts = Validate(content);
        if (texts.IsFailure)
        {
            return Result<bool>.Failure(texts.Error);
        }

        var changed = Severity != content.Severity
            || Audience != content.Audience
            || StartsAtUtc != content.StartsAtUtc
            || EndsAtUtc != content.EndsAtUtc
            || Dismissible != content.Dismissible
            || _texts.Count != texts.Value.Count
            || _texts.Zip(texts.Value).Any(pair =>
                pair.First.Language != pair.Second.Language
                || pair.First.Title != pair.Second.Title
                || pair.First.Body != pair.Second.Body);

        if (changed)
        {
            Apply(content, texts.Value);
            UpdatedAtUtc = now;
        }

        return Result<bool>.Success(changed);
    }

    /// <summary>Whether it shows at <paramref name="now"/>.</summary>
    public bool IsActiveAt(DateTimeOffset now) =>
        (StartsAtUtc is null || StartsAtUtc <= now) && (EndsAtUtc is null || EndsAtUtc > now);

    /// <summary>
    /// The text in <paramref name="language"/>, else in its base language (<c>pt</c> for <c>pt-br</c>), else in
    /// <paramref name="defaultLanguage"/>, else the first one written.
    /// </summary>
    public AnnouncementText TextFor(string? language, string? defaultLanguage)
    {
        var wanted = language?.Trim().ToLowerInvariant();
        var baseLanguage = wanted?.Split('-')[0];
        var fallback = defaultLanguage?.Trim().ToLowerInvariant();

        return _texts.FirstOrDefault(text => text.Language == wanted)
            ?? _texts.FirstOrDefault(text => text.Language == baseLanguage)
            ?? _texts.FirstOrDefault(text => text.Language == fallback)
            ?? _texts[0];
    }

    private void Apply(AnnouncementContent content, IReadOnlyList<AnnouncementText> texts)
    {
        Severity = content.Severity;
        Audience = content.Audience;
        StartsAtUtc = content.StartsAtUtc?.ToUniversalTime();
        EndsAtUtc = content.EndsAtUtc?.ToUniversalTime();
        Dismissible = content.Dismissible;
        _texts.Clear();
        _texts.AddRange(texts);
    }

    private static Result<IReadOnlyList<AnnouncementText>> Validate(AnnouncementContent content)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (!Enum.IsDefined(content.Severity) || !Enum.IsDefined(content.Audience))
        {
            return Result<IReadOnlyList<AnnouncementText>>.Failure(AnnouncementErrors.Invalid);
        }

        if (content.StartsAtUtc is { } starts && content.EndsAtUtc is { } ends && ends <= starts)
        {
            return Result<IReadOnlyList<AnnouncementText>>.Failure(AnnouncementErrors.WindowInvalid);
        }

        if (content.Texts is null || content.Texts.Count == 0)
        {
            return Result<IReadOnlyList<AnnouncementText>>.Failure(AnnouncementErrors.TextRequired);
        }

        if (content.Texts.Count > MaxTexts)
        {
            return Result<IReadOnlyList<AnnouncementText>>.Failure(AnnouncementErrors.Invalid);
        }

        var texts = new List<AnnouncementText>();
        foreach (var text in content.Texts)
        {
            var language = text.Language?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(language)
                || language.Length > AnnouncementText.LanguageMaxLength
                || !language.All(character => character is (>= 'a' and <= 'z') or '-')
                || texts.Exists(existing => existing.Language == language))
            {
                return Result<IReadOnlyList<AnnouncementText>>.Failure(AnnouncementErrors.LanguageInvalid);
            }

            var title = text.Title?.Trim();
            if (string.IsNullOrEmpty(title))
            {
                return Result<IReadOnlyList<AnnouncementText>>.Failure(AnnouncementErrors.TitleRequired);
            }

            var body = string.IsNullOrWhiteSpace(text.Body) ? null : text.Body.Trim();
            if (title.Length > AnnouncementText.TitleMaxLength || body?.Length > AnnouncementText.BodyMaxLength)
            {
                return Result<IReadOnlyList<AnnouncementText>>.Failure(AnnouncementErrors.TextTooLong);
            }

            texts.Add(new AnnouncementText(language, title, body));
        }

        return Result<IReadOnlyList<AnnouncementText>>.Success(texts);
    }
}

/// <summary>Stable, machine-readable failure codes for announcements.</summary>
public static class AnnouncementErrors
{
    private const string Prefix = "announcements.";

    public static Error NotFound { get; } =
        Error.NotFound(Prefix + "not_found", "The announcement does not exist.");

    public static Error ManageForbidden { get; } =
        Error.Forbidden(
            Prefix + "manage_forbidden",
            "Managing announcements requires an administrator account or the announcements page.");

    public static Error Invalid { get; } =
        Error.Validation(Prefix + "invalid", "The announcement's kind or audience is not valid.");

    public static Error WindowInvalid { get; } =
        Error.Validation(Prefix + "window_invalid", "An announcement must end after it starts.");

    public static Error TextRequired { get; } =
        Error.Validation(Prefix + "text_required", "An announcement needs a text in at least one language.");

    public static Error LanguageInvalid { get; } =
        Error.Validation(Prefix + "language_invalid", "Each text needs a different, valid language code.");

    public static Error TitleRequired { get; } =
        Error.Validation(Prefix + "title_required", "Each text needs a title.");

    public static Error TextTooLong { get; } =
        Error.Validation(
            Prefix + "text_too_long",
            $"A title is at most {AnnouncementText.TitleMaxLength} characters and a message at most {AnnouncementText.BodyMaxLength}.");
}
