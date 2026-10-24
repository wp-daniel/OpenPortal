using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.SharedKernel.Messaging;
using OpenPortal.Web.Localization;
using OpenPortal.Web.Resources;

namespace OpenPortal.Web.Email;

/// <summary>
/// Writes the Identity module's account emails from the resource files (<c>email.&lt;kind&gt;.subject</c>,
/// <c>.greeting</c>, <c>.body</c>, <c>.action</c>) in the recipient's language, links them to the portal's public
/// address and queues them for delivery (<see cref="IOutbox"/>, delivered by <see cref="EmailOutboxHandler"/>).
/// </summary>
public sealed partial class HostAccountMessaging : IAccountMessaging
{
    private readonly IEmailSender _sender;
    private readonly IOutbox _outbox;
    private readonly IStringLocalizer<Messages> _localizer;
    private readonly ILanguageCatalog _languages;
    private readonly IHttpContextAccessor _httpContext;
    private readonly EmailOptions _options;
    private readonly ILogger<HostAccountMessaging> _logger;

    public HostAccountMessaging(
        IEmailSender sender,
        IOutbox outbox,
        IStringLocalizer<Messages> localizer,
        ILanguageCatalog languages,
        IHttpContextAccessor httpContext,
        IOptions<EmailOptions> options,
        ILogger<HostAccountMessaging> logger)
    {
        _sender = sender;
        _outbox = outbox;
        _localizer = localizer;
        _languages = languages;
        _httpContext = httpContext;
        _options = options.Value;
        _logger = logger;
    }

    public Task<bool> CanSendEmailAsync(CancellationToken cancellationToken) => _sender.IsConfiguredAsync(cancellationToken);

    public string LinkTo(string pathAndQuery)
    {
        ArgumentException.ThrowIfNullOrEmpty(pathAndQuery);

        var configured = _options.PublicUrl?.Trim().TrimEnd('/');
        if (!string.IsNullOrEmpty(configured))
        {
            return configured + pathAndQuery;
        }

        var request = _httpContext.HttpContext?.Request;

        return request is null ? pathAndQuery : $"{request.Scheme}://{request.Host}{request.PathBase}{pathAndQuery}";
    }

    public async Task<bool> SendAsync(AccountMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (!await _sender.IsConfiguredAsync(cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        try
        {
            // Queued, not sent here: the request does not wait for the mail server, and a server that is down
            // costs a delay (the queue retries) instead of the email.
            await _outbox.EnqueueAsync(EmailOutboxHandler.Draft(Render(message)), cancellationToken).ConfigureAwait(false);

            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031 // A message that cannot be queued is reported as "not sent", never as a failed request.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            LogDeliveryFailed(exception, message.Kind);
            return false;
        }
    }

    /// <summary>
    /// The email in the recipient's language (when the portal offers it, else the request's), linked to the
    /// portal's public address.
    /// </summary>
    public EmailMessage Render(AccountMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var previous = CultureInfo.CurrentUICulture;
        if (message.Language is { } language && _languages.IsSupported(language))
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
        }

        try
        {
            var link = LinkTo(message.LinkPath);
            var values = new Dictionary<string, string?>(message.Values, StringComparer.Ordinal);

            if (values.TryGetValue("expires", out var expires)
                && DateTimeOffset.TryParse(expires, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at))
            {
                values["expires"] = at.ToString("D", CultureInfo.CurrentUICulture);
            }

            var prefix = $"email.{message.Kind}.";
            var subject = Text(prefix + "subject", values);
            var greeting = Text(prefix + "greeting", values);
            var body = Text(prefix + "body", values);
            var action = Text(prefix + "action", values);
            var note = values.GetValueOrDefault("message");
            var footer = Text("email.footer", values);

            return new EmailMessage(
                message.ToEmail,
                message.ToName,
                subject,
                PlainText(greeting, body, note, action, link, footer),
                Html(subject, greeting, body, note, action, link, footer));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    /// <summary>The resource text with its <c>{name}</c> placeholders filled; empty values leave the placeholder out.</summary>
    private string Text(string key, IReadOnlyDictionary<string, string?> values)
    {
        var entry = _localizer[key];
        var text = entry.ResourceNotFound ? string.Empty : entry.Value;

        foreach (var (name, value) in values)
        {
            // "Hello {name}," without a name reads "Hello,", not "Hello ,".
            text = string.IsNullOrWhiteSpace(value)
                ? text.Replace(" {" + name + "}", string.Empty, StringComparison.Ordinal)
                    .Replace("{" + name + "}", string.Empty, StringComparison.Ordinal)
                : text.Replace("{" + name + "}", value, StringComparison.Ordinal);
        }

        // A placeholder nobody filled is left out the same way.
        text = System.Text.RegularExpressions.Regex.Replace(text, @" ?\{[A-Za-z]+\}", string.Empty);

        return text.Trim();
    }

    private static string PlainText(string greeting, string body, string? note, string action, string link, string footer)
    {
        var text = new StringBuilder();
        text.AppendLine(greeting).AppendLine();
        text.AppendLine(body).AppendLine();

        if (!string.IsNullOrWhiteSpace(note))
        {
            text.AppendLine($"\"{note}\"").AppendLine();
        }

        text.AppendLine($"{action}: {link}").AppendLine();
        text.AppendLine("--").AppendLine(footer);

        return text.ToString();
    }

    private static string Html(string subject, string greeting, string body, string? note, string action, string link, string footer)
    {
        var encode = HtmlEncoder.Default;
        var quote = string.IsNullOrWhiteSpace(note)
            ? string.Empty
            : $"""<blockquote style="margin:16px 0;padding:8px 16px;border-left:3px solid #d4d4d4;color:#525252">{encode.Encode(note)}</blockquote>""";

        return $"""
            <!doctype html>
            <html>
            <head><meta charset="utf-8"><title>{encode.Encode(subject)}</title></head>
            <body style="margin:0;padding:24px;background:#fafafa;font-family:-apple-system,Segoe UI,Roboto,Helvetica,Arial,sans-serif;color:#171717">
              <div style="max-width:520px;margin:0 auto;background:#ffffff;border:1px solid #e5e5e5;border-radius:12px;padding:32px">
                <p style="margin:0 0 16px;font-size:16px">{encode.Encode(greeting)}</p>
                <p style="margin:0 0 16px;font-size:14px;line-height:1.6">{encode.Encode(body)}</p>
                {quote}
                <p style="margin:24px 0"><a href="{encode.Encode(link)}" style="display:inline-block;background:#171717;color:#ffffff;text-decoration:none;padding:10px 18px;border-radius:8px;font-size:14px">{encode.Encode(action)}</a></p>
                <p style="margin:0;font-size:12px;color:#737373;word-break:break-all">{encode.Encode(link)}</p>
              </div>
              <p style="max-width:520px;margin:16px auto 0;font-size:12px;color:#a3a3a3;text-align:center">{encode.Encode(footer)}</p>
            </body>
            </html>
            """;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The {Kind} email could not be queued.")]
    private partial void LogDeliveryFailed(Exception exception, string kind);
}
