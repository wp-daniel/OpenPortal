using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Identity.Domain.Legal;

/// <summary>Which document: the privacy notice or the terms of use.</summary>
public enum LegalDocumentKind
{
    Privacy = 0,
    Terms = 1,
}

/// <summary>Where a version stands.</summary>
public enum LegalDocumentStatus
{
    /// <summary>Being written; visible to its editors only.</summary>
    Draft = 0,

    /// <summary>The version in force: shown to everyone and, when it requires it, accepted by each account.</summary>
    Published = 1,

    /// <summary>A version that was in force before the current one; kept with its acceptances as a record.</summary>
    Superseded = 2,
}

/// <summary>A version's text in one language.</summary>
public sealed class LegalDocumentText
{
    public const int LanguageMaxLength = 10;
    public const int TitleMaxLength = 200;
    public const int BodyMaxLength = 100_000;

    // Required by EF Core.
    private LegalDocumentText()
    {
    }

    internal LegalDocumentText(string language, string title, string body)
    {
        Language = language;
        Title = title;
        Body = body;
    }

    public string Language { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    /// <summary>Markdown (rendered without raw HTML).</summary>
    public string Body { get; private set; } = string.Empty;
}

/// <summary>What an editor writes in a draft.</summary>
public sealed record LegalDocumentContent(bool RequiresAcceptance, IReadOnlyList<LegalDocumentTextContent> Texts);

/// <summary>One language's text, as written.</summary>
public sealed record LegalDocumentTextContent(string? Language, string? Title, string? Body);

/// <summary>
/// One version of the privacy notice or the terms of use. A version is written as a draft, then published, which
/// supersedes the version in force; a published version never changes again, so an acceptance always refers to
/// the exact text that was accepted. Publishing a version that requires acceptance asks every account to accept it
/// at its next sign-in.
/// </summary>
public sealed class LegalDocument
{
    public const int MaxTexts = 20;

    private readonly List<LegalDocumentText> _texts = [];

    // Required by EF Core.
    private LegalDocument()
    {
    }

    public Guid Id { get; private set; }

    public LegalDocumentKind Kind { get; private set; }

    /// <summary>1 for the first version of its kind, then one more for each new draft.</summary>
    public int Version { get; private set; }

    public LegalDocumentStatus Status { get; private set; }

    /// <summary>Whether each account must accept this version (at registration, and at the next sign-in).</summary>
    public bool RequiresAcceptance { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public DateTimeOffset? PublishedAtUtc { get; private set; }

    public IReadOnlyList<LegalDocumentText> Texts => _texts;

    public static Result<LegalDocument> CreateDraft(
        Guid id,
        LegalDocumentKind kind,
        int version,
        LegalDocumentContent content,
        DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(Guid.Empty, id);
        ArgumentOutOfRangeException.ThrowIfLessThan(version, 1);

        if (!Enum.IsDefined(kind))
        {
            return Result<LegalDocument>.Failure(LegalErrors.KindInvalid);
        }

        var texts = Validate(content);
        if (texts.IsFailure)
        {
            return Result<LegalDocument>.Failure(texts.Error);
        }

        var document = new LegalDocument
        {
            Id = id,
            Kind = kind,
            Version = version,
            Status = LegalDocumentStatus.Draft,
            RequiresAcceptance = content.RequiresAcceptance,
            CreatedAtUtc = now,
        };
        document._texts.AddRange(texts.Value);

        return Result<LegalDocument>.Success(document);
    }

    /// <summary>Replaces a draft's content. Returns whether anything changed; a published version cannot change.</summary>
    public Result<bool> UpdateDraft(LegalDocumentContent content, DateTimeOffset now)
    {
        if (Status != LegalDocumentStatus.Draft)
        {
            return Result<bool>.Failure(LegalErrors.NotDraft);
        }

        var texts = Validate(content);
        if (texts.IsFailure)
        {
            return Result<bool>.Failure(texts.Error);
        }

        var changed = RequiresAcceptance != content.RequiresAcceptance
            || _texts.Count != texts.Value.Count
            || _texts.Zip(texts.Value).Any(pair =>
                pair.First.Language != pair.Second.Language
                || pair.First.Title != pair.Second.Title
                || pair.First.Body != pair.Second.Body);

        if (changed)
        {
            RequiresAcceptance = content.RequiresAcceptance;
            _texts.Clear();
            _texts.AddRange(texts.Value);
            UpdatedAtUtc = now;
        }

        return Result<bool>.Success(changed);
    }

    /// <summary>Puts a draft in force. The caller supersedes the version it replaces.</summary>
    public Result Publish(DateTimeOffset now)
    {
        if (Status != LegalDocumentStatus.Draft)
        {
            return Result.Failure(LegalErrors.NotDraft);
        }

        Status = LegalDocumentStatus.Published;
        PublishedAtUtc = now;
        return Result.Success();
    }

    /// <summary>Retires the version in force when a newer one is published.</summary>
    public void Supersede()
    {
        if (Status == LegalDocumentStatus.Published)
        {
            Status = LegalDocumentStatus.Superseded;
        }
    }

    /// <summary>
    /// The text in <paramref name="language"/>, else its base language, else <paramref name="defaultLanguage"/>,
    /// else the first one written.
    /// </summary>
    public LegalDocumentText TextFor(string? language, string? defaultLanguage)
    {
        var wanted = language?.Trim().ToLowerInvariant();
        var baseLanguage = wanted?.Split('-')[0];
        var fallback = defaultLanguage?.Trim().ToLowerInvariant();

        return _texts.FirstOrDefault(text => text.Language == wanted)
            ?? _texts.FirstOrDefault(text => text.Language == baseLanguage)
            ?? _texts.FirstOrDefault(text => text.Language == fallback)
            ?? _texts[0];
    }

    private static Result<IReadOnlyList<LegalDocumentText>> Validate(LegalDocumentContent content)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (content.Texts is null || content.Texts.Count == 0)
        {
            return Result<IReadOnlyList<LegalDocumentText>>.Failure(LegalErrors.TextRequired);
        }

        if (content.Texts.Count > MaxTexts)
        {
            return Result<IReadOnlyList<LegalDocumentText>>.Failure(LegalErrors.LanguageInvalid);
        }

        var texts = new List<LegalDocumentText>();
        foreach (var text in content.Texts)
        {
            var language = text.Language?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(language)
                || language.Length > LegalDocumentText.LanguageMaxLength
                || !language.All(character => character is (>= 'a' and <= 'z') or '-')
                || texts.Exists(existing => existing.Language == language))
            {
                return Result<IReadOnlyList<LegalDocumentText>>.Failure(LegalErrors.LanguageInvalid);
            }

            var title = text.Title?.Trim();
            var body = text.Body?.Trim();
            if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(body))
            {
                return Result<IReadOnlyList<LegalDocumentText>>.Failure(LegalErrors.TextRequired);
            }

            if (title.Length > LegalDocumentText.TitleMaxLength || body.Length > LegalDocumentText.BodyMaxLength)
            {
                return Result<IReadOnlyList<LegalDocumentText>>.Failure(LegalErrors.TextTooLong);
            }

            texts.Add(new LegalDocumentText(language, title, body));
        }

        return Result<IReadOnlyList<LegalDocumentText>>.Success(texts);
    }
}

/// <summary>An account's acceptance of one version of a legal document.</summary>
public sealed class LegalAcceptance
{
    // Required by EF Core.
    private LegalAcceptance()
    {
    }

    public LegalAcceptance(Guid userId, Guid documentId, DateTimeOffset acceptedAtUtc)
    {
        UserId = userId;
        DocumentId = documentId;
        AcceptedAtUtc = acceptedAtUtc;
    }

    public Guid UserId { get; private set; }

    public Guid DocumentId { get; private set; }

    public DateTimeOffset AcceptedAtUtc { get; private set; }
}

/// <summary>Stable, machine-readable failure codes for legal documents.</summary>
public static class LegalErrors
{
    private const string Prefix = "legal.";

    public static Error NotFound { get; } =
        Error.NotFound(Prefix + "not_found", "The document does not exist.");

    public static Error KindInvalid { get; } =
        Error.Validation(Prefix + "kind_invalid", "The document kind must be privacy or terms.");

    public static Error NotDraft { get; } =
        Error.Conflict(Prefix + "not_draft", "Only a draft can be changed, published or deleted.");

    public static Error DraftExists { get; } =
        Error.Conflict(Prefix + "draft_exists", "There is already a draft of this document; edit or delete it first.");

    public static Error TextRequired { get; } =
        Error.Validation(Prefix + "text_required", "A document needs a title and a text in at least one language.");

    public static Error LanguageInvalid { get; } =
        Error.Validation(Prefix + "language_invalid", "Each text needs a different, valid language code.");

    public static Error TextTooLong { get; } =
        Error.Validation(
            Prefix + "text_too_long",
            $"A title is at most {LegalDocumentText.TitleMaxLength} characters and a text at most {LegalDocumentText.BodyMaxLength}.");

    public static Error AcceptanceRequired { get; } =
        Error.Validation(Prefix + "acceptance_required", "The current privacy notice and terms of use must be accepted.");
}
