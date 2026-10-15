namespace OpenPortal.Identity.Application.Contracts;

/// <summary>A version's text in one language.</summary>
/// <param name="Body">Markdown.</param>
public sealed record LegalDocumentTextDto(string Language, string Title, string Body);

/// <summary>A version of the privacy notice or the terms of use, as its editors see it.</summary>
/// <param name="Kind"><c>privacy</c> or <c>terms</c>.</param>
/// <param name="Status"><c>draft</c>, <c>published</c> or <c>superseded</c>.</param>
/// <param name="AcceptanceCount">How many accounts accepted this version.</param>
public sealed record LegalDocumentDto(
    Guid Id,
    string Kind,
    int Version,
    string Status,
    bool RequiresAcceptance,
    IReadOnlyList<LegalDocumentTextDto> Texts,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    DateTimeOffset? PublishedAtUtc,
    int AcceptanceCount);

/// <summary>The version in force, in the reader's language.</summary>
public sealed record PublishedLegalDocumentDto(
    Guid Id,
    string Kind,
    int Version,
    string Language,
    string Title,
    string Body,
    bool RequiresAcceptance,
    DateTimeOffset PublishedAtUtc);

/// <summary>A version in force the signed-in account has not accepted yet.</summary>
public sealed record PendingLegalDocumentDto(Guid Id, string Kind, int Version, string Title);

/// <summary>Which documents are published, so the client can link them.</summary>
public sealed record LegalInfoDto(bool Privacy, bool Terms)
{
    public static LegalInfoDto None { get; } = new(false, false);
}

/// <summary>Creates or replaces a draft.</summary>
public sealed class SaveLegalDocumentRequest
{
    public bool RequiresAcceptance { get; init; } = true;

    public IReadOnlyList<LegalDocumentTextDto> Texts { get; init; } = [];
}

/// <summary>The versions the signed-in account accepts, by id.</summary>
public sealed class AcceptLegalDocumentsRequest
{
    public IReadOnlyList<Guid> DocumentIds { get; init; } = [];
}

/// <summary>The wire names of the legal document enums.</summary>
public static class LegalNames
{
    public const string Privacy = "privacy";
    public const string Terms = "terms";
    public const string Draft = "draft";
    public const string Published = "published";
    public const string Superseded = "superseded";
}
