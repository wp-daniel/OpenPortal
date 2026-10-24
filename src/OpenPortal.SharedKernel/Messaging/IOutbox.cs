namespace OpenPortal.SharedKernel.Messaging;

/// <summary>
/// A durable queue of work to do outside the request that caused it: an email to send, a webhook to call, a
/// logout to tell an application about. A message is written to the database first and delivered afterwards by
/// a dispatcher that retries with a growing delay, so a mail server or an application that is briefly down
/// costs a delay instead of the message, and the request does not wait for it.
/// <para>
/// Messages are handled by the <see cref="IOutboxHandler"/> registered for their <see cref="OutboxMessageDraft.Type"/>.
/// Delivery is at least once: a handler must tolerate seeing the same message again.
/// </para>
/// </summary>
public interface IOutbox
{
    Task EnqueueAsync(OutboxMessageDraft message, CancellationToken cancellationToken);

    /// <summary>Several messages in one write.</summary>
    Task EnqueueAsync(IReadOnlyCollection<OutboxMessageDraft> messages, CancellationToken cancellationToken);
}

/// <summary>A message to queue.</summary>
/// <param name="Type">Which <see cref="IOutboxHandler"/> delivers it (e.g. <c>email</c>).</param>
/// <param name="Payload">What the handler needs, usually JSON. Never store a secret the handler could look up instead.</param>
/// <param name="Description">A short label shown to administrators in the delivery queue (a recipient, an address).</param>
public sealed record OutboxMessageDraft(string Type, string Payload, string? Description = null);

/// <summary>Delivers the outbox messages of one type. Throwing schedules another attempt.</summary>
public interface IOutboxHandler
{
    string Type { get; }

    Task HandleAsync(string payload, CancellationToken cancellationToken);
}

/// <summary>
/// Thrown by a handler when trying again cannot help (the payload is unusable, the target no longer exists): the
/// message is marked failed at once instead of being retried.
/// </summary>
public sealed class OutboxPermanentFailureException : Exception
{
    public OutboxPermanentFailureException()
    {
    }

    public OutboxPermanentFailureException(string message)
        : base(message)
    {
    }

    public OutboxPermanentFailureException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
