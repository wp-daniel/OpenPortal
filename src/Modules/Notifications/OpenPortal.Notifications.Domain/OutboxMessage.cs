using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Notifications.Domain;

public enum OutboxMessageStatus
{
    /// <summary>Waiting for its first or next attempt.</summary>
    Pending = 0,

    /// <summary>Delivered.</summary>
    Delivered = 1,

    /// <summary>Gave up: too many attempts, or a failure that retrying cannot fix. An administrator may retry it.</summary>
    Failed = 2,
}

/// <summary>
/// One message of the delivery queue (see <c>IOutbox</c>). The dispatcher claims it with a lease, so several
/// instances never deliver the same message at the same time, and reschedules it with a growing delay after a
/// failure.
/// </summary>
public sealed class OutboxMessage
{
    public const int TypeMaxLength = 64;
    public const int DescriptionMaxLength = 256;
    public const int ErrorMaxLength = 2000;
    public const int OwnerMaxLength = 64;

    /// <summary>The delays between attempts; the last one repeats.</summary>
    private static readonly TimeSpan[] Backoff =
    [
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(3),
    ];

    // Required by EF Core.
    private OutboxMessage()
    {
    }

    public Guid Id { get; private set; }

    public string Type { get; private set; } = string.Empty;

    public string Payload { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public OutboxMessageStatus Status { get; private set; }

    public int Attempts { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>Not attempted before this moment.</summary>
    public DateTimeOffset AvailableAtUtc { get; private set; }

    /// <summary>While in the future, an instance is working on the message and the others leave it alone.</summary>
    public DateTimeOffset? LockedUntilUtc { get; private set; }

    public string? LockedBy { get; private set; }

    public DateTimeOffset? LastAttemptAtUtc { get; private set; }

    public DateTimeOffset? DeliveredAtUtc { get; private set; }

    public string? LastError { get; private set; }

    public static Result<OutboxMessage> Create(Guid id, string? type, string? payload, string? description, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(Guid.Empty, id);

        var trimmedType = type?.Trim();
        if (string.IsNullOrEmpty(trimmedType) || trimmedType.Length > TypeMaxLength)
        {
            return Result<OutboxMessage>.Failure(OutboxErrors.TypeInvalid);
        }

        if (string.IsNullOrEmpty(payload))
        {
            return Result<OutboxMessage>.Failure(OutboxErrors.PayloadRequired);
        }

        return Result<OutboxMessage>.Success(new OutboxMessage
        {
            Id = id,
            Type = trimmedType,
            Payload = payload,
            Description = Truncate(description?.Trim(), DescriptionMaxLength),
            Status = OutboxMessageStatus.Pending,
            CreatedAtUtc = now,
            AvailableAtUtc = now,
        });
    }

    /// <summary>Whether the message would be picked up at <paramref name="now"/>.</summary>
    public bool IsDueAt(DateTimeOffset now) =>
        Status == OutboxMessageStatus.Pending
        && AvailableAtUtc <= now
        && (LockedUntilUtc is null || LockedUntilUtc < now);

    public void MarkDelivered(DateTimeOffset now)
    {
        Attempts++;
        Status = OutboxMessageStatus.Delivered;
        LastAttemptAtUtc = now;
        DeliveredAtUtc = now;
        LastError = null;
        LockedUntilUtc = null;
        LockedBy = null;
    }

    /// <summary>
    /// Records a failed attempt: retried after the next delay of the backoff, or marked failed once
    /// <paramref name="maxAttempts"/> is reached or when <paramref name="permanent"/>.
    /// </summary>
    public void MarkAttemptFailed(string? error, DateTimeOffset now, int maxAttempts, bool permanent)
    {
        Attempts++;
        LastAttemptAtUtc = now;
        LastError = Truncate(error, ErrorMaxLength);
        LockedUntilUtc = null;
        LockedBy = null;

        if (permanent || Attempts >= maxAttempts)
        {
            Status = OutboxMessageStatus.Failed;
            return;
        }

        AvailableAtUtc = now + Backoff[Math.Min(Attempts - 1, Backoff.Length - 1)];
    }

    /// <summary>An administrator asks for another try: back in the queue, due now, with its attempts kept.</summary>
    public Result Retry(DateTimeOffset now)
    {
        if (Status == OutboxMessageStatus.Delivered)
        {
            return Result.Failure(OutboxErrors.AlreadyDelivered);
        }

        Status = OutboxMessageStatus.Pending;
        AvailableAtUtc = now;
        LockedUntilUtc = null;
        LockedBy = null;

        return Result.Success();
    }

    private static string? Truncate(string? value, int length) =>
        value is null ? null : value.Length <= length ? value : value[..length];
}

/// <summary>Stable, machine-readable failure codes for the delivery queue.</summary>
public static class OutboxErrors
{
    private const string Prefix = "outbox.";

    public static Error TypeInvalid { get; } =
        Error.Validation(Prefix + "type_invalid", $"A queued message needs a type of at most {OutboxMessage.TypeMaxLength} characters.");

    public static Error PayloadRequired { get; } =
        Error.Validation(Prefix + "payload_required", "A queued message needs a payload.");

    public static Error NotFound { get; } =
        Error.NotFound(Prefix + "not_found", "The queued message does not exist.");

    public static Error AlreadyDelivered { get; } =
        Error.Conflict(Prefix + "already_delivered", "The message was delivered already.");

    public static Error ManageForbidden { get; } =
        Error.Forbidden(Prefix + "manage_forbidden", "Only an administrator can manage email and the delivery queue.");
}
