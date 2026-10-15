using OpenPortal.Identity.Application.Contracts;
using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Identity.Application.Abstractions;

/// <summary>
/// The privacy notice and the terms of use: their versions, the one in force, and who accepted which. Editing
/// re-checks the caller (<see cref="UserAdministrationOperation.ManageLegalDocuments"/>); reading the version in
/// force is open to everyone; accepting is always the signed-in caller's own.
/// </summary>
public interface ILegalDocumentService
{
    /// <summary>The version of <paramref name="kind"/> in force, in the reader's language; null when none is published.</summary>
    Task<PublishedLegalDocumentDto?> GetCurrentAsync(string kind, CancellationToken cancellationToken);

    /// <summary>Which kinds have a version in force.</summary>
    Task<LegalInfoDto> GetInfoAsync(CancellationToken cancellationToken);

    /// <summary>The versions in force that require acceptance and the account has not accepted.</summary>
    Task<IReadOnlyList<PendingLegalDocumentDto>> PendingAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Records the signed-in caller's acceptance of the given versions; each must be in force and require
    /// acceptance. Accepting again changes nothing.
    /// </summary>
    Task<Result<IReadOnlyList<PendingLegalDocumentDto>>> AcceptAsync(AcceptLegalDocumentsRequest request, CancellationToken cancellationToken);

    /// <summary>Every version of both documents, newest first.</summary>
    Task<Result<IReadOnlyList<LegalDocumentDto>>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Starts the next version of <paramref name="kind"/> as a draft. One draft per kind at a time.</summary>
    Task<Result<LegalDocumentDto>> CreateDraftAsync(string kind, SaveLegalDocumentRequest request, CancellationToken cancellationToken);

    Task<Result<LegalDocumentDto>> UpdateDraftAsync(Guid documentId, SaveLegalDocumentRequest request, CancellationToken cancellationToken);

    /// <summary>Puts a draft in force, superseding the version it replaces.</summary>
    Task<Result<LegalDocumentDto>> PublishAsync(Guid documentId, CancellationToken cancellationToken);

    Task<Result> DeleteDraftAsync(Guid documentId, CancellationToken cancellationToken);
}
