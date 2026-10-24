using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Notifications.Application.Abstractions;
using StackExchange.Redis;

namespace OpenPortal.Web.Notifications;

/// <summary>
/// Wakes the notification streams (<c>/api/notifications/stream</c>) of the people whose notifications changed. On
/// one instance it signals in memory; with Redis configured it publishes on a channel every instance listens to, so
/// a notice written on one reaches a browser connected to another.
/// </summary>
public sealed partial class NotificationHub : INotificationPublisher, IDisposable
{
    public const string RedisChannel = "openportal:notifications";

    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, Channel<bool>>> _subscribers = new();
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<NotificationHub> _logger;
    private ChannelMessageQueue? _subscription;

    public NotificationHub(IServiceProvider services, ILogger<NotificationHub> logger)
    {
        _redis = services.GetService<IConnectionMultiplexer>();
        _logger = logger;
    }

    /// <summary>Starts listening to the other instances (Redis), once.</summary>
    public async Task StartAsync()
    {
        if (_redis is null || _subscription is not null)
        {
            return;
        }

        _subscription = await _redis.GetSubscriber().SubscribeAsync(RedisChannel.ToRedisChannel()).ConfigureAwait(false);
        _subscription.OnMessage(message =>
        {
            foreach (var part in ((string?)message.Message ?? string.Empty).Split(','))
            {
                if (Guid.TryParse(part, out var userId))
                {
                    Signal(userId);
                }
            }
        });
    }

    public async Task PublishAsync(IReadOnlyCollection<Guid> recipientIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipientIds);

        if (recipientIds.Count == 0)
        {
            return;
        }

        if (_redis is null)
        {
            foreach (var userId in recipientIds)
            {
                Signal(userId);
            }

            return;
        }

        try
        {
            await _redis.GetSubscriber()
                .PublishAsync(RedisChannel.ToRedisChannel(), string.Join(',', recipientIds))
                .ConfigureAwait(false);
        }
        catch (RedisException exception)
        {
            // The browsers still poll; a missed signal costs a delay, not a notification.
            LogPublishFailed(exception);
        }
    }

    /// <summary>A reader that yields each time the user's notifications change, until the subscription is disposed.</summary>
    public Subscription Subscribe(Guid userId)
    {
        var channel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });
        var id = Guid.NewGuid();
        _subscribers.GetOrAdd(userId, _ => new ConcurrentDictionary<Guid, Channel<bool>>())[id] = channel;

        return new Subscription(channel.Reader, () =>
        {
            if (_subscribers.TryGetValue(userId, out var channels))
            {
                channels.TryRemove(id, out _);
            }
        });
    }

    public void Dispose() => _subscription?.Unsubscribe();

    private void Signal(Guid userId)
    {
        if (_subscribers.TryGetValue(userId, out var channels))
        {
            foreach (var channel in channels.Values)
            {
                channel.Writer.TryWrite(true);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not publish a notification change through Redis.")]
    private partial void LogPublishFailed(Exception exception);

    public sealed class Subscription : IDisposable
    {
        private readonly Action _dispose;

        internal Subscription(ChannelReader<bool> reader, Action dispose)
        {
            Reader = reader;
            _dispose = dispose;
        }

        public ChannelReader<bool> Reader { get; }

        public void Dispose() => _dispose();
    }
}

internal static class RedisChannelNames
{
    public static RedisChannel ToRedisChannel(this string name) => RedisChannel.Literal(name);
}

/// <summary>Starts the hub's cross-instance subscription with the host.</summary>
internal sealed class NotificationHubStarter : IHostedService
{
    private readonly NotificationHub _hub;

    public NotificationHubStarter(NotificationHub hub)
    {
        _hub = hub;
    }

    public Task StartAsync(CancellationToken cancellationToken) => _hub.StartAsync();

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// After a successful change to the caller's notifications (read, unread, deleted), wakes their other open tabs.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class PublishesNotificationChangeAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var executed = await next().ConfigureAwait(false);

        if (executed.Exception is null
            && executed.Result is not ObjectResult { StatusCode: >= 400 }
            && context.HttpContext.RequestServices.GetRequiredService<ICurrentUser>().UserId is { } userId)
        {
            await context.HttpContext.RequestServices.GetRequiredService<INotificationPublisher>()
                .PublishAsync([userId], context.HttpContext.RequestAborted)
                .ConfigureAwait(false);
        }
    }
}
