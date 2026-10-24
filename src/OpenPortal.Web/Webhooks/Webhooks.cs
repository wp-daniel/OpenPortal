using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Access.Application.Auditing;
using OpenPortal.Access.Application.Contracts;
using OpenPortal.Access.Domain.Applications;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Application.Auditing;
using OpenPortal.SharedKernel.Auditing;
using OpenPortal.SharedKernel.Messaging;

namespace OpenPortal.Web.Webhooks;

/// <summary>
/// Turns what the modules record into the events applications subscribed to. Every state change already goes
/// through <see cref="IAuditTrail"/> with its subject and details, so the events come from there without the modules
/// knowing about webhooks: this decorator records first, then queues one delivery per application concerned.
/// </summary>
public sealed partial class WebhookAuditTrail : IAuditTrail
{
    /// <summary>Audit action → webhook event, and how to find the applications it concerns.</summary>
    private static readonly IReadOnlyDictionary<string, (string Event, Audience Audience)> Events =
        new Dictionary<string, (string, Audience)>(StringComparer.Ordinal)
        {
            [IdentityAuditActions.UserUpdated] = (WebhookEventTypes.UserUpdated, Audience.User),
            [IdentityAuditActions.ProfileUpdated] = (WebhookEventTypes.UserUpdated, Audience.User),
            [IdentityAuditActions.UserDeleted] = (WebhookEventTypes.UserDeleted, Audience.User),
            [AccessAuditActions.UserGranted] = (WebhookEventTypes.AccessGranted, Audience.Application),
            [AccessAuditActions.GroupGranted] = (WebhookEventTypes.AccessGranted, Audience.Application),
            [AccessAuditActions.UserRevoked] = (WebhookEventTypes.AccessRevoked, Audience.Application),
            [AccessAuditActions.GroupRevoked] = (WebhookEventTypes.AccessRevoked, Audience.Application),
            [AccessAuditActions.UserRolesChanged] = (WebhookEventTypes.AccessRolesChanged, Audience.Application),
            [AccessAuditActions.GroupRolesChanged] = (WebhookEventTypes.AccessRolesChanged, Audience.Application),
            [AccessAuditActions.GroupMemberAdded] = (WebhookEventTypes.GroupMemberAdded, Audience.Group),
            [AccessAuditActions.GroupMemberRemoved] = (WebhookEventTypes.GroupMemberRemoved, Audience.Group),
            [AccessAuditActions.OrganizationMemberAdded] = (WebhookEventTypes.OrganizationMemberAdded, Audience.User),
            [AccessAuditActions.OrganizationMemberRemoved] = (WebhookEventTypes.OrganizationMemberRemoved, Audience.User),
            [AccessAuditActions.OrganizationMemberRoleChanged] = (WebhookEventTypes.OrganizationMemberRoleChanged, Audience.User),
        };

    private readonly IAuditTrail _inner;
    private readonly WebhookPublisher _publisher;
    private readonly IWebhookDirectory _directory;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<WebhookAuditTrail> _logger;

    public WebhookAuditTrail(
        IAuditTrail inner,
        WebhookPublisher publisher,
        IWebhookDirectory directory,
        ICurrentUser currentUser,
        ILogger<WebhookAuditTrail> logger)
    {
        _inner = inner;
        _publisher = publisher;
        _directory = directory;
        _currentUser = currentUser;
        _logger = logger;
    }

    public enum Audience
    {
        /// <summary>The applications the user (the event's subject, or the caller for their own account) can open.</summary>
        User,

        /// <summary>The application named by the event's <c>clientId</c> detail.</summary>
        Application,

        /// <summary>The applications the group named by the <c>groupId</c> detail has been given.</summary>
        Group,
    }

    public async Task RecordAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        await _inner.RecordAsync(auditEvent, cancellationToken).ConfigureAwait(false);

        if (auditEvent.Outcome != AuditOutcome.Success || !Events.TryGetValue(auditEvent.Action, out var mapping))
        {
            return;
        }

        try
        {
            var targets = await TargetsAsync(auditEvent, mapping.Event, mapping.Audience, cancellationToken).ConfigureAwait(false);
            if (targets.Count == 0)
            {
                return;
            }

            var subject = auditEvent.Target
                ?? (_currentUser.UserId is { } self ? new AuditSubject(AuditSubjectTypes.User, self.ToString(), _currentUser.Email) : null);

            var data = new Dictionary<string, object?>
            {
                ["subject"] = subject is null ? null : new { type = subject.Type, id = subject.Id, label = subject.Label },
                ["details"] = auditEvent.Details,
            };

            await _publisher.PublishAsync(mapping.Event, data, targets, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031 // Like the audit itself, an event is best effort: it never fails the change it reports.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            LogQueueFailed(exception, mapping.Event);
        }
    }

    private Task<IReadOnlyList<WebhookTarget>> TargetsAsync(AuditEvent auditEvent, string eventType, Audience audience, CancellationToken cancellationToken)
    {
        string? Detail(string name) => auditEvent.Details?.GetValueOrDefault(name);

        switch (audience)
        {
            case Audience.Application when Detail("clientId") is { Length: > 0 } clientId:
                return _directory.ForApplicationAsync(clientId, eventType, cancellationToken);

            case Audience.Group when Guid.TryParse(Detail("groupId"), out var groupId):
                return _directory.ForGroupAsync(groupId, eventType, cancellationToken);

            case Audience.User:
                var userId = auditEvent.Target is { Type: AuditSubjectTypes.User } target && Guid.TryParse(target.Id, out var id)
                    ? id
                    : auditEvent.Target is null ? _currentUser.UserId : null;

                return userId is { } user
                    ? _directory.ForUserAsync(user, eventType, cancellationToken)
                    : Task.FromResult<IReadOnlyList<WebhookTarget>>([]);

            default:
                return Task.FromResult<IReadOnlyList<WebhookTarget>>([]);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The {Event} webhook could not be queued.")]
    private partial void LogQueueFailed(Exception exception, string @event);
}

/// <summary>Queues one delivery per application: the body is fixed now, the signature is made at each attempt.</summary>
public sealed class WebhookPublisher
{
    private readonly IOutbox _outbox;
    private readonly TimeProvider _time;

    public WebhookPublisher(IOutbox outbox, TimeProvider time)
    {
        _outbox = outbox;
        _time = time;
    }

    public Task PublishAsync(string eventType, object data, IReadOnlyList<WebhookTarget> targets, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(targets);

        var timestamp = _time.GetUtcNow();
        var messages = targets
            .Select(target =>
            {
                var id = "msg_" + Guid.NewGuid().ToString("N");
                var body = JsonSerializer.Serialize(
                    new { type = eventType, timestamp, application = target.ClientId, data },
                    JsonSerializerOptions.Web);

                return new OutboxMessageDraft(
                    WebhookHandler.MessageType,
                    JsonSerializer.Serialize(new WebhookDelivery(target.ClientId, id, eventType, body)),
                    $"{target.ClientId}: {eventType}");
            })
            .ToList();

        return _outbox.EnqueueAsync(messages, cancellationToken);
    }
}

public sealed record WebhookDelivery(string ClientId, string MessageId, string EventType, string Body);

/// <summary>
/// Posts a queued event (outbox type <c>webhook</c>) to the application's webhook URL, signed as the Standard Webhooks
/// specification describes: <c>webhook-id</c>, <c>webhook-timestamp</c> and <c>webhook-signature</c> =
/// <c>v1,</c> + base64(HMAC-SHA256(secret, id.timestamp.body)). The URL and secret are read at each attempt, so a
/// rotated secret or a moved endpoint applies to retries too.
/// </summary>
public sealed class WebhookHandler : IOutboxHandler
{
    public const string MessageType = "webhook";

    public const string HttpClientName = "OpenPortal.Webhooks";

    private readonly IWebhookDirectory _directory;
    private readonly IHttpClientFactory _http;
    private readonly TimeProvider _time;

    public WebhookHandler(IWebhookDirectory directory, IHttpClientFactory http, TimeProvider time)
    {
        _directory = directory;
        _http = http;
        _time = time;
    }

    public string Type => MessageType;

    public async Task HandleAsync(string payload, CancellationToken cancellationToken)
    {
        var delivery = JsonSerializer.Deserialize<WebhookDelivery>(payload)
            ?? throw new OutboxPermanentFailureException("The queued webhook is empty.");

        var target = await _directory.FindAsync(delivery.ClientId, cancellationToken).ConfigureAwait(false)
            ?? throw new OutboxPermanentFailureException("The application's webhook was turned off or lost its secret.");

        var timestamp = _time.GetUtcNow().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

        using var request = new HttpRequestMessage(HttpMethod.Post, target.Url)
        {
            Content = new StringContent(delivery.Body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("webhook-id", delivery.MessageId);
        request.Headers.Add("webhook-timestamp", timestamp);
        request.Headers.Add("webhook-signature", "v1," + Sign(target.Secret, delivery.MessageId, timestamp, delivery.Body));

        using var client = _http.CreateClient(HttpClientName);
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"The webhook answered {(int)response.StatusCode}.", null, response.StatusCode);
        }
    }

    /// <summary>base64(HMAC-SHA256(key, <c>{id}.{timestamp}.{body}</c>)), the key being the secret after <c>whsec_</c>.</summary>
    public static string Sign(string secret, string messageId, string timestamp, string body)
    {
        ArgumentNullException.ThrowIfNull(secret);

        var key = Convert.FromBase64String(secret.StartsWith("whsec_", StringComparison.Ordinal) ? secret["whsec_".Length..] : secret);
        var signed = Encoding.UTF8.GetBytes($"{messageId}.{timestamp}.{body}");

        return Convert.ToBase64String(HMACSHA256.HashData(key, signed));
    }
}
