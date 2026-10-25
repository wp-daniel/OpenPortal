using System.Globalization;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Notifications.Application.Abstractions;
using OpenPortal.Notifications.Application.Contracts;
using OpenPortal.SharedKernel.Messaging;
using OpenPortal.Web.Email;

namespace OpenPortal.Web.Notifications;

/// <summary>
/// Emails notifications with the same texts the notification center shows (<c>notification.&lt;type&gt;.title</c> and
/// <c>.body</c>), in the recipient's language, through the delivery queue like every other email. One notification is
/// one email; the daily digest lists them.
/// </summary>
internal sealed class HostNotificationMailer : INotificationMailer
{
    private readonly IEmailSender _sender;
    private readonly IOutbox _outbox;
    private readonly IUserLookupService _users;
    private readonly HostAccountMessaging _messaging;
    private readonly ILanguageCatalog _languages;
    private readonly IHttpContextAccessor _httpContext;

    public HostNotificationMailer(
        IEmailSender sender,
        IOutbox outbox,
        IUserLookupService users,
        HostAccountMessaging messaging,
        ILanguageCatalog languages,
        IHttpContextAccessor httpContext)
    {
        _httpContext = httpContext;
        _sender = sender;
        _outbox = outbox;
        _users = users;
        _messaging = messaging;
        _languages = languages;
    }

    public Task<bool> CanSendAsync(CancellationToken cancellationToken) => _sender.IsConfiguredAsync(cancellationToken);

    public async Task SendAsync(Guid recipientId, IReadOnlyList<NotificationMailItem> items, bool digest, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (items.Count == 0 || await _users.FindAsync(recipientId, cancellationToken).ConfigureAwait(false) is not { IsActive: true } user
            || string.IsNullOrEmpty(user.Email))
        {
            return;
        }

        // The recipient's language; else the request's; else (the daily digest, sent by the portal on its own) the
        // portal's default, never the server's.
        var previous = CultureInfo.CurrentUICulture;
        if (user.Language is { } language && _languages.IsSupported(language))
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
        }
        else if (_httpContext.HttpContext is null)
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(_languages.DefaultLanguage);
        }

        try
        {
            var name = new Dictionary<string, string?> { ["name"] = user.DisplayName };
            var footer = _messaging.Text("email.notification.footer", name);
            string subject;
            string body;
            string link;

            if (digest)
            {
                subject = _messaging.Text("email.digest.subject", new Dictionary<string, string?> { ["count"] = items.Count.ToString(CultureInfo.CurrentUICulture) });
                body = string.Join("\n", items.Select(item => "• " + Line(item)));
                link = _messaging.LinkTo("/notifications");
            }
            else
            {
                var item = items[0];
                subject = _messaging.Text($"notification.{item.Type}.title", item.Parameters);
                body = _messaging.Text($"notification.{item.Type}.body", item.Parameters);
                link = _messaging.LinkTo(item.Link ?? "/notifications");
            }

            var greeting = _messaging.Text("email.notification.greeting", name);
            var action = _messaging.Text(digest ? "email.digest.action" : "email.notification.action", name);

            var message = new EmailMessage(
                user.Email,
                user.DisplayName,
                subject,
                HostAccountMessaging.PlainText(greeting, body, null, action, link, footer),
                HostAccountMessaging.Html(subject, greeting, body, null, action, link, footer));

            await _outbox.EnqueueAsync(EmailOutboxHandler.Draft(message), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    private string Line(NotificationMailItem item)
    {
        var title = _messaging.Text($"notification.{item.Type}.title", item.Parameters);
        var body = _messaging.Text($"notification.{item.Type}.body", item.Parameters);

        return string.IsNullOrEmpty(body) ? title : $"{title}: {body}";
    }
}
