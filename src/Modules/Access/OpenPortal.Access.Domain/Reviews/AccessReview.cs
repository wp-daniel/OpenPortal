using OpenPortal.SharedKernel.Results;
using OpenPortal.SharedKernel.Text;

namespace OpenPortal.Access.Domain.Reviews;

public enum AccessReviewStatus
{
    Open = 0,
    Completed = 1,
    Cancelled = 2,
}

public enum AccessReviewDecision
{
    /// <summary>Nobody has decided yet.</summary>
    Pending = 0,

    /// <summary>The access is still needed.</summary>
    Keep = 1,

    /// <summary>The access goes when the review is completed.</summary>
    Revoke = 2,
}

public enum AccessReviewSubjectType
{
    User = 0,
    Group = 1,
}

/// <summary>
/// A periodic check of who can open an application (access recertification): a snapshot of its user and group grants,
/// each to be confirmed or revoked by a reviewer. Completing the review removes what was marked for revocation and,
/// when <see cref="RevokeUndecided"/>, whatever nobody confirmed.
/// </summary>
public sealed class AccessReview
{
    public const int NameMaxLength = 120;
    public const int NoteMaxLength = 500;

    private readonly List<AccessReviewItem> _items = [];

    private AccessReview()
    {
    }

    public Guid Id { get; private set; }

    public Guid ApplicationId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public AccessReviewStatus Status { get; private set; }

    /// <summary>Whether completing the review also revokes the grants nobody decided on ("use it or lose it").</summary>
    public bool RevokeUndecided { get; private set; }

    public Guid? CreatedById { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? DueAtUtc { get; private set; }

    public DateTimeOffset? ClosedAtUtc { get; private set; }

    public Guid? ClosedById { get; private set; }

    public IReadOnlyList<AccessReviewItem> Items => _items;

    public static Result<AccessReview> Start(
        Guid id,
        Guid applicationId,
        string? name,
        DateTimeOffset? dueAtUtc,
        bool revokeUndecided,
        Guid? createdById,
        IEnumerable<AccessReviewItemSeed> items,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(items);

        var trimmed = TextRules.Normalise(name);
        if (trimmed is null || trimmed.Length > NameMaxLength)
        {
            return Result<AccessReview>.Failure(AccessReviewErrors.NameInvalid);
        }

        if (dueAtUtc is { } due && due <= now)
        {
            return Result<AccessReview>.Failure(AccessReviewErrors.DueInPast);
        }

        var review = new AccessReview
        {
            Id = id,
            ApplicationId = applicationId,
            Name = trimmed,
            Status = AccessReviewStatus.Open,
            RevokeUndecided = revokeUndecided,
            CreatedById = createdById,
            CreatedAtUtc = now,
            DueAtUtc = dueAtUtc,
        };

        foreach (var seed in items)
        {
            review._items.Add(new AccessReviewItem(Guid.NewGuid(), id, seed));
        }

        return review._items.Count == 0
            ? Result<AccessReview>.Failure(AccessReviewErrors.NothingToReview)
            : Result<AccessReview>.Success(review);
    }

    public Result Decide(Guid itemId, AccessReviewDecision decision, string? note, Guid? reviewerId, DateTimeOffset now)
    {
        if (Status != AccessReviewStatus.Open)
        {
            return Result.Failure(AccessReviewErrors.NotOpen);
        }

        if (!Enum.IsDefined(decision))
        {
            return Result.Failure(AccessReviewErrors.DecisionInvalid);
        }

        var trimmedNote = TextRules.Normalise(note);
        if (trimmedNote?.Length > NoteMaxLength)
        {
            return Result.Failure(AccessReviewErrors.NoteTooLong);
        }

        var item = _items.FirstOrDefault(candidate => candidate.Id == itemId);
        if (item is null)
        {
            return Result.Failure(AccessReviewErrors.ItemNotFound);
        }

        item.Decide(decision, trimmedNote, reviewerId, now);

        return Result.Success();
    }

    /// <summary>Closes the review and returns the items whose access must now be removed.</summary>
    public Result<IReadOnlyList<AccessReviewItem>> Complete(Guid? byId, DateTimeOffset now)
    {
        if (Status != AccessReviewStatus.Open)
        {
            return Result<IReadOnlyList<AccessReviewItem>>.Failure(AccessReviewErrors.NotOpen);
        }

        Status = AccessReviewStatus.Completed;
        ClosedAtUtc = now;
        ClosedById = byId;

        return Result<IReadOnlyList<AccessReviewItem>>.Success(_items
            .Where(item => item.Decision == AccessReviewDecision.Revoke
                || (RevokeUndecided && item.Decision == AccessReviewDecision.Pending))
            .ToList());
    }

    public Result Cancel(Guid? byId, DateTimeOffset now)
    {
        if (Status != AccessReviewStatus.Open)
        {
            return Result.Failure(AccessReviewErrors.NotOpen);
        }

        Status = AccessReviewStatus.Cancelled;
        ClosedAtUtc = now;
        ClosedById = byId;

        return Result.Success();
    }
}

/// <summary>What a review starts from: one grant of the application.</summary>
public sealed record AccessReviewItemSeed(AccessReviewSubjectType SubjectType, Guid SubjectId, string SubjectLabel, IReadOnlyList<string> Roles);

/// <summary>One grant under review and the decision about it.</summary>
public sealed class AccessReviewItem
{
    public const int LabelMaxLength = 256;

    private AccessReviewItem()
    {
    }

    internal AccessReviewItem(Guid id, Guid reviewId, AccessReviewItemSeed seed)
    {
        Id = id;
        ReviewId = reviewId;
        SubjectType = seed.SubjectType;
        SubjectId = seed.SubjectId;
        SubjectLabel = seed.SubjectLabel.Length > LabelMaxLength ? seed.SubjectLabel[..LabelMaxLength] : seed.SubjectLabel;
        Roles = seed.Roles;
    }

    public Guid Id { get; private set; }

    public Guid ReviewId { get; private set; }

    public AccessReviewSubjectType SubjectType { get; private set; }

    public Guid SubjectId { get; private set; }

    /// <summary>The user's address or the group's name when the review started.</summary>
    public string SubjectLabel { get; private set; } = string.Empty;

    public IReadOnlyList<string> Roles { get; private set; } = [];

    public AccessReviewDecision Decision { get; private set; }

    public string? Note { get; private set; }

    public Guid? DecidedById { get; private set; }

    public DateTimeOffset? DecidedAtUtc { get; private set; }

    internal void Decide(AccessReviewDecision decision, string? note, Guid? reviewerId, DateTimeOffset now)
    {
        Decision = decision;
        Note = note;
        DecidedById = decision == AccessReviewDecision.Pending ? null : reviewerId;
        DecidedAtUtc = decision == AccessReviewDecision.Pending ? null : now;
    }
}

/// <summary>Stable, machine-readable failure codes for access reviews.</summary>
public static class AccessReviewErrors
{
    private const string Prefix = "access.review_";

    public static Error NotFound { get; } = Error.NotFound(Prefix + "not_found", "The access review does not exist.");

    public static Error ItemNotFound { get; } = Error.NotFound(Prefix + "item_not_found", "The review has no such entry.");

    public static Error NameInvalid { get; } =
        Error.Validation(Prefix + "name_invalid", $"A review needs a name of at most {AccessReview.NameMaxLength} characters.");

    public static Error DueInPast { get; } = Error.Validation(Prefix + "due_in_past", "The due date must be in the future.");

    public static Error NothingToReview { get; } = Error.Conflict(Prefix + "nothing_to_review", "Nobody has been given this application: there is nothing to review.");

    public static Error NotOpen { get; } = Error.Conflict(Prefix + "not_open", "The review is already closed.");

    public static Error DecisionInvalid { get; } = Error.Validation(Prefix + "decision_invalid", "The decision must be pending, keep or revoke.");

    public static Error NoteTooLong { get; } = Error.Validation(Prefix + "note_too_long", $"A note is at most {AccessReview.NoteMaxLength} characters.");

    public static Error Forbidden { get; } = Error.Forbidden(Prefix + "forbidden", "Reviewing access takes an administrator account or the access reviews page.");
}
