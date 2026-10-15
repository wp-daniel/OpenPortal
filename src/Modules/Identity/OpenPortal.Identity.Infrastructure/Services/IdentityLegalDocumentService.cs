using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Application.Auditing;
using OpenPortal.Identity.Application.Contracts;
using OpenPortal.Identity.Domain.Legal;
using OpenPortal.Identity.Domain.Users;
using OpenPortal.Identity.Infrastructure.Persistence;
using OpenPortal.SharedKernel.Auditing;
using OpenPortal.SharedKernel.Results;
using OpenPortal.SharedKernel.Time;

namespace OpenPortal.Identity.Infrastructure.Services;

/// <summary>The privacy notice and the terms of use. See <see cref="ILegalDocumentService"/>.</summary>
internal sealed class IdentityLegalDocumentService : ILegalDocumentService
{
    private readonly IdentityDbContext _db;
    private readonly AdministrationGuard _guard;
    private readonly ICurrentUser _currentUser;
    private readonly ILanguageCatalog _languages;
    private readonly IClock _clock;
    private readonly IAuditTrail _audit;

    public IdentityLegalDocumentService(
        IdentityDbContext db,
        AdministrationGuard guard,
        ICurrentUser currentUser,
        ILanguageCatalog languages,
        IClock clock,
        IAuditTrail audit)
    {
        _db = db;
        _guard = guard;
        _currentUser = currentUser;
        _languages = languages;
        _clock = clock;
        _audit = audit;
    }

    private static string ReaderLanguage => CultureInfo.CurrentUICulture.Name;

    public async Task<PublishedLegalDocumentDto?> GetCurrentAsync(string kind, CancellationToken cancellationToken)
    {
        if (ParseKind(kind) is not { } parsed)
        {
            return null;
        }

        var document = await Published()
            .FirstOrDefaultAsync(candidate => candidate.Kind == parsed, cancellationToken)
            .ConfigureAwait(false);

        if (document is null)
        {
            return null;
        }

        var text = document.TextFor(ReaderLanguage, _languages.DefaultLanguage);

        return new PublishedLegalDocumentDto(
            document.Id,
            KindName(document.Kind),
            document.Version,
            text.Language,
            text.Title,
            text.Body,
            document.RequiresAcceptance,
            document.PublishedAtUtc ?? document.CreatedAtUtc);
    }

    public async Task<LegalInfoDto> GetInfoAsync(CancellationToken cancellationToken)
    {
        var kinds = await _db.LegalDocuments
            .AsNoTracking()
            .Where(document => document.Status == LegalDocumentStatus.Published)
            .Select(document => document.Kind)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new LegalInfoDto(kinds.Contains(LegalDocumentKind.Privacy), kinds.Contains(LegalDocumentKind.Terms));
    }

    public async Task<IReadOnlyList<PendingLegalDocumentDto>> PendingAsync(Guid userId, CancellationToken cancellationToken)
    {
        var accepted = _db.LegalAcceptances.Where(acceptance => acceptance.UserId == userId).Select(acceptance => acceptance.DocumentId);

        var pending = await Published()
            .Where(document => document.RequiresAcceptance && !accepted.Contains(document.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return pending
            .OrderBy(document => document.Kind)
            .Select(document => new PendingLegalDocumentDto(
                document.Id,
                KindName(document.Kind),
                document.Version,
                document.TextFor(ReaderLanguage, _languages.DefaultLanguage).Title))
            .ToList();
    }

    public async Task<Result<IReadOnlyList<PendingLegalDocumentDto>>> AcceptAsync(
        AcceptLegalDocumentsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_currentUser.UserId is not Guid userId)
        {
            return Result<IReadOnlyList<PendingLegalDocumentDto>>.Failure(UserErrors.NotAuthenticated);
        }

        var ids = (request.DocumentIds ?? []).ToHashSet();

        // Only versions in force can be accepted; a stale id (a version superseded meanwhile) is refused so the
        // client shows the new text instead of recording consent to one nobody read.
        var current = await RequiredIdsAsync(cancellationToken).ConfigureAwait(false);
        if (ids.Count == 0 || !ids.All(current.Contains))
        {
            return Result<IReadOnlyList<PendingLegalDocumentDto>>.Failure(LegalErrors.AcceptanceRequired);
        }

        var pending = await PendingAsync(userId, cancellationToken).ConfigureAwait(false);
        var accepted = pending.Where(document => ids.Contains(document.Id)).ToList();
        if (accepted.Count > 0)
        {
            await RecordAsync(userId, accepted.Select(document => document.Id).ToList(), cancellationToken).ConfigureAwait(false);

            await _audit.RecordAsync(
                    AuditEvent.Succeeded(
                        IdentityAuditActions.LegalAccepted,
                        details: new Dictionary<string, string?>
                        {
                            ["documents"] = string.Join(", ", accepted.Select(document => $"{document.Kind} v{document.Version}")),
                        }),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return Result<IReadOnlyList<PendingLegalDocumentDto>>.Success(
            await PendingAsync(userId, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<IReadOnlyList<LegalDocumentDto>>> ListAsync(CancellationToken cancellationToken)
    {
        var guard = await _guard.EnsureCanAsync(UserAdministrationOperation.ManageLegalDocuments, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<IReadOnlyList<LegalDocumentDto>>.Failure(guard.Error);
        }

        var documents = await _db.LegalDocuments.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        var counts = await _db.LegalAcceptances
            .GroupBy(acceptance => acceptance.DocumentId)
            .Select(group => new { DocumentId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(entry => entry.DocumentId, entry => entry.Count, cancellationToken)
            .ConfigureAwait(false);

        return Result<IReadOnlyList<LegalDocumentDto>>.Success(documents
            .OrderBy(document => document.Kind)
            .ThenByDescending(document => document.Version)
            .Select(document => ToDto(document, counts.GetValueOrDefault(document.Id)))
            .ToList());
    }

    public async Task<Result<LegalDocumentDto>> CreateDraftAsync(
        string kind,
        SaveLegalDocumentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var guard = await _guard.EnsureCanAsync(UserAdministrationOperation.ManageLegalDocuments, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<LegalDocumentDto>.Failure(guard.Error);
        }

        if (ParseKind(kind) is not { } parsed)
        {
            return Result<LegalDocumentDto>.Failure(LegalErrors.KindInvalid);
        }

        var versions = await _db.LegalDocuments
            .Where(document => document.Kind == parsed)
            .Select(document => new { document.Version, document.Status })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (versions.Exists(version => version.Status == LegalDocumentStatus.Draft))
        {
            return Result<LegalDocumentDto>.Failure(LegalErrors.DraftExists);
        }

        var created = LegalDocument.CreateDraft(
            Guid.NewGuid(),
            parsed,
            versions.Count == 0 ? 1 : versions.Max(version => version.Version) + 1,
            ToContent(request),
            _clock.UtcNow);
        if (created.IsFailure)
        {
            return Result<LegalDocumentDto>.Failure(created.Error);
        }

        _db.LegalDocuments.Add(created.Value);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<LegalDocumentDto>.Success(ToDto(created.Value, 0));
    }

    public async Task<Result<LegalDocumentDto>> UpdateDraftAsync(
        Guid documentId,
        SaveLegalDocumentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var found = await FindForEditAsync(documentId, cancellationToken).ConfigureAwait(false);
        if (found.IsFailure)
        {
            return Result<LegalDocumentDto>.Failure(found.Error);
        }

        var updated = found.Value.UpdateDraft(ToContent(request), _clock.UtcNow);
        if (updated.IsFailure)
        {
            return Result<LegalDocumentDto>.Failure(updated.Error);
        }

        if (updated.Value)
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return Result<LegalDocumentDto>.Success(ToDto(found.Value, 0));
    }

    public async Task<Result<LegalDocumentDto>> PublishAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var found = await FindForEditAsync(documentId, cancellationToken).ConfigureAwait(false);
        if (found.IsFailure)
        {
            return Result<LegalDocumentDto>.Failure(found.Error);
        }

        var document = found.Value;
        var published = document.Publish(_clock.UtcNow);
        if (published.IsFailure)
        {
            return Result<LegalDocumentDto>.Failure(published.Error);
        }

        var previous = await _db.LegalDocuments
            .Where(candidate => candidate.Kind == document.Kind && candidate.Status == LegalDocumentStatus.Published && candidate.Id != document.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var version in previous)
        {
            version.Supersede();
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _audit.RecordAsync(
                AuditEvent.Succeeded(
                    IdentityAuditActions.LegalDocumentPublished,
                    Subject(document),
                    new Dictionary<string, string?>
                    {
                        ["version"] = document.Version.ToString(CultureInfo.InvariantCulture),
                        ["requiresAcceptance"] = document.RequiresAcceptance ? "true" : "false",
                    }),
                cancellationToken)
            .ConfigureAwait(false);

        return Result<LegalDocumentDto>.Success(ToDto(document, 0));
    }

    public async Task<Result> DeleteDraftAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var found = await FindForEditAsync(documentId, cancellationToken).ConfigureAwait(false);
        if (found.IsFailure)
        {
            return Result.Failure(found.Error);
        }

        if (found.Value.Status != LegalDocumentStatus.Draft)
        {
            return Result.Failure(LegalErrors.NotDraft);
        }

        _db.LegalDocuments.Remove(found.Value);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    /// <summary>
    /// Checks the versions a new account accepted while registering or accepting an invitation: every version in
    /// force that requires acceptance must be among them. Returns the ids to record once the account exists.
    /// </summary>
    internal async Task<Result<IReadOnlyList<Guid>>> RequireAcceptedAsync(
        IReadOnlyCollection<Guid>? acceptedIds,
        CancellationToken cancellationToken)
    {
        var required = await RequiredIdsAsync(cancellationToken).ConfigureAwait(false);

        return required.All(id => acceptedIds?.Contains(id) == true)
            ? Result<IReadOnlyList<Guid>>.Success(required)
            : Result<IReadOnlyList<Guid>>.Failure(LegalErrors.AcceptanceRequired);
    }

    /// <summary>Records acceptances already checked (by <see cref="RequireAcceptedAsync"/> or <see cref="AcceptAsync"/>).</summary>
    internal async Task RecordAsync(Guid userId, IReadOnlyList<Guid> documentIds, CancellationToken cancellationToken)
    {
        if (documentIds.Count == 0)
        {
            return;
        }

        var already = await _db.LegalAcceptances
            .Where(acceptance => acceptance.UserId == userId && documentIds.Contains(acceptance.DocumentId))
            .Select(acceptance => acceptance.DocumentId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var now = _clock.UtcNow;
        foreach (var id in documentIds.Except(already))
        {
            _db.LegalAcceptances.Add(new LegalAcceptance(userId, id, now));
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<List<Guid>> RequiredIdsAsync(CancellationToken cancellationToken) =>
        await _db.LegalDocuments
            .Where(document => document.Status == LegalDocumentStatus.Published && document.RequiresAcceptance)
            .Select(document => document.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    private async Task<Result<LegalDocument>> FindForEditAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var guard = await _guard.EnsureCanAsync(UserAdministrationOperation.ManageLegalDocuments, cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<LegalDocument>.Failure(guard.Error);
        }

        var document = await _db.LegalDocuments
            .FirstOrDefaultAsync(candidate => candidate.Id == documentId, cancellationToken)
            .ConfigureAwait(false);

        return document is null ? Result<LegalDocument>.Failure(LegalErrors.NotFound) : Result<LegalDocument>.Success(document);
    }

    private IQueryable<LegalDocument> Published() =>
        _db.LegalDocuments.AsNoTracking().Where(document => document.Status == LegalDocumentStatus.Published);

    private static LegalDocumentContent ToContent(SaveLegalDocumentRequest request) => new(
        request.RequiresAcceptance,
        (request.Texts ?? []).Select(text => new LegalDocumentTextContent(text?.Language, text?.Title, text?.Body)).ToList());

    private static AuditSubject Subject(LegalDocument document) =>
        new(AuditSubjectTypes.LegalDocument, document.Id.ToString(), $"{KindName(document.Kind)} v{document.Version}");

    private static LegalDocumentKind? ParseKind(string? kind) => kind?.Trim().ToLowerInvariant() switch
    {
        LegalNames.Privacy => LegalDocumentKind.Privacy,
        LegalNames.Terms => LegalDocumentKind.Terms,
        _ => null,
    };

    private static string KindName(LegalDocumentKind kind) => kind == LegalDocumentKind.Privacy ? LegalNames.Privacy : LegalNames.Terms;

    private static LegalDocumentDto ToDto(LegalDocument document, int acceptances) => new(
        document.Id,
        KindName(document.Kind),
        document.Version,
        document.Status switch
        {
            LegalDocumentStatus.Published => LegalNames.Published,
            LegalDocumentStatus.Superseded => LegalNames.Superseded,
            _ => LegalNames.Draft,
        },
        document.RequiresAcceptance,
        document.Texts.Select(text => new LegalDocumentTextDto(text.Language, text.Title, text.Body)).ToList(),
        document.CreatedAtUtc,
        document.UpdatedAtUtc,
        document.PublishedAtUtc,
        acceptances);
}
