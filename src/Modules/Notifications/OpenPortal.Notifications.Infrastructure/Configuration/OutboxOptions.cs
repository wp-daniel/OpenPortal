namespace OpenPortal.Notifications.Infrastructure.Configuration;

/// <summary>The delivery queue's settings, bound from the <c>Outbox</c> configuration section.</summary>
public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    /// <summary>How often each instance looks for due messages when nothing woke it earlier.</summary>
    public int PollSeconds { get; set; } = 10;

    /// <summary>How many messages one pass claims.</summary>
    public int BatchSize { get; set; } = 20;

    /// <summary>Attempts before a message is marked failed (the delays grow from 30 seconds to 3 hours).</summary>
    public int MaxAttempts { get; set; } = 10;

    /// <summary>How long a claim lasts: an instance that dies while delivering frees its messages after this.</summary>
    public int LeaseSeconds { get; set; } = 120;

    /// <summary>How long delivered messages are kept for the queue page; 0 keeps them.</summary>
    public int DeliveredRetentionDays { get; set; } = 7;

    /// <summary>How long failed messages are kept; 0 keeps them.</summary>
    public int FailedRetentionDays { get; set; } = 30;

    /// <summary>
    /// Delivers each message in the request that queued it (still recorded, still retried on failure) instead of
    /// in the background. For tests, which read what was sent right after the request.
    /// </summary>
    public bool DispatchInline { get; set; }
}
