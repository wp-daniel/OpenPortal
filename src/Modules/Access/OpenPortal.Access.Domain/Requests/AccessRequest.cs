using OpenPortal.SharedKernel.Results;
using OpenPortal.SharedKernel.Text;

namespace OpenPortal.Access.Domain.Requests;

/// <summary>Where an access request is in its life. Only a <see cref="Pending"/> one can change.</summary>
public enum AccessRequestStatus
{
    /// <summary>Waiting for a reviewer.</summary>
    Pending = 0,

    /// <summary>A reviewer granted the application; the grant exists from that moment.</summary>
    Approved = 1,

    /// <summary>A reviewer refused it.</summary>
    Rejected = 2,

    /// <summary>The requester withdrew it before anyone decided.</summary>
    Cancelled = 3,
}

/// <summary>
/// A user asking to open an application they have not been given, for a reviewer to approve or reject.
/// <para>
/// The request never grants anything by itself: approving it creates an ordinary user grant (with the roles the
/// reviewer chose), so access is withdrawn the usual way and the access rule stays in one place. A user has at
/// most one pending request per application.
/// </para>
/// </summary>
public sealed class AccessRequest
{
    public const int ReasonMaxLength = 500;
    public const int NoteMaxLength = 500;

    // Required by EF Core.
    private AccessRequest()
    {
    }

    public Guid Id { get; private set; }

    public Guid ApplicationId { get; private set; }

    /// <summary>The requester.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Why the requester needs the application, in their words.</summary>
    public string? Reason { get; private set; }

    public AccessRequestStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>When it was approved, rejected or cancelled.</summary>
    public DateTimeOffset? DecidedAtUtc { get; private set; }

    /// <summary>The reviewer who approved or rejected it; null while pending and when cancelled.</summary>
    public Guid? DecidedByUserId { get; private set; }

    /// <summary>The reviewer's message to the requester.</summary>
    public string? DecisionNote { get; private set; }

    /// <summary>The role keys the approval granted.</summary>
    public IReadOnlyList<string> GrantedRoles { get; private set; } = [];

    public bool IsPending => Status == AccessRequestStatus.Pending;

    public static Result<AccessRequest> Submit(Guid id, Guid applicationId, Guid userId, string? reason, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(Guid.Empty, id);
        ArgumentOutOfRangeException.ThrowIfEqual(Guid.Empty, applicationId);
        ArgumentOutOfRangeException.ThrowIfEqual(Guid.Empty, userId);

        var validated = ValidateText(reason, ReasonMaxLength, AccessErrors.RequestReasonTooLong);
        if (validated.IsFailure)
        {
            return Result<AccessRequest>.Failure(validated.Error);
        }

        return Result<AccessRequest>.Success(new AccessRequest
        {
            Id = id,
            ApplicationId = applicationId,
            UserId = userId,
            Reason = validated.Value,
            Status = AccessRequestStatus.Pending,
            CreatedAtUtc = now,
        });
    }

    /// <summary>Records the approval; the caller has created the grant already.</summary>
    public Result Approve(Guid reviewerId, IEnumerable<string> grantedRoles, string? note, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(grantedRoles);

        return Decide(AccessRequestStatus.Approved, reviewerId, note, now, grantedRoles);
    }

    public Result Reject(Guid reviewerId, string? note, DateTimeOffset now) =>
        Decide(AccessRequestStatus.Rejected, reviewerId, note, now, []);

    /// <summary>Withdraws a pending request; only its requester may.</summary>
    public Result Cancel(DateTimeOffset now)
    {
        if (!IsPending)
        {
            return Result.Failure(AccessErrors.RequestNotPending);
        }

        Status = AccessRequestStatus.Cancelled;
        DecidedAtUtc = now;

        return Result.Success();
    }

    private Result Decide(
        AccessRequestStatus status,
        Guid reviewerId,
        string? note,
        DateTimeOffset now,
        IEnumerable<string> grantedRoles)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(Guid.Empty, reviewerId);

        if (!IsPending)
        {
            return Result.Failure(AccessErrors.RequestNotPending);
        }

        var validated = ValidateNote(note);
        if (validated.IsFailure)
        {
            return Result.Failure(validated.Error);
        }

        Status = status;
        DecidedByUserId = reviewerId;
        DecisionNote = validated.Value;
        DecidedAtUtc = now;
        GrantedRoles = grantedRoles.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

        return Result.Success();
    }

    /// <summary>Trims a reviewer's note and checks its length, without needing a request.</summary>
    public static Result<string?> ValidateNote(string? note) =>
        ValidateText(note, NoteMaxLength, AccessErrors.RequestNoteTooLong);

    private static Result<string?> ValidateText(string? value, int maxLength, Error tooLong)
    {
        var trimmed = TextRules.Normalise(value);

        return trimmed?.Length > maxLength ? Result<string?>.Failure(tooLong) : Result<string?>.Success(trimmed);
    }
}
