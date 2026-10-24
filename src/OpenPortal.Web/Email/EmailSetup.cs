using System.Text.Json;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using OpenPortal.Notifications.Application.Abstractions;
using OpenPortal.Notifications.Application.Contracts;
using OpenPortal.SharedKernel.Messaging;

namespace OpenPortal.Web.Email;

/// <summary>
/// Email delivery settings, bound from the <c>Email</c> section. The SMTP server and sender saved from the portal
/// (Settings → Email) take precedence; these apply while the portal has none of its own.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>
    /// The portal's public address (<c>https://portal.example.com</c>), used for the links in emails. Without it
    /// links use the address of the request that caused the email; set it in production, where that request may
    /// come through a proxy or carry a forged Host header.
    /// </summary>
    public string? PublicUrl { get; set; }

    /// <summary>The sender address. Required for delivery.</summary>
    public string? From { get; set; }

    public string FromName { get; set; } = "OpenPortal";

    /// <summary>
    /// A folder where each email is written as an <c>.eml</c> file instead of being sent: for development and for
    /// a relay that picks files up. Used when no SMTP host is configured.
    /// </summary>
    public string? PickupDirectory { get; set; }

    public SmtpSettings Smtp { get; set; } = new();

    public sealed class SmtpSettings
    {
        public string? Host { get; set; }

        public int Port { get; set; } = 587;

        /// <summary><c>Auto</c> (STARTTLS when offered, implicit TLS on 465), <c>StartTls</c>, <c>SslOnConnect</c> or <c>None</c>.</summary>
        public string Security { get; set; } = "Auto";

        public string? Username { get; set; }

        public string? Password { get; set; }
    }

    internal bool UsesSmtp => !string.IsNullOrWhiteSpace(Smtp.Host);

    internal bool UsesPickup => !string.IsNullOrWhiteSpace(PickupDirectory);

    internal bool IsConfigured => !string.IsNullOrWhiteSpace(From) && (UsesSmtp || UsesPickup);
}

/// <summary>An email ready to go: the texts are already in the recipient's language.</summary>
public sealed record EmailMessage(string To, string? ToName, string Subject, string TextBody, string HtmlBody);

/// <summary>Where an email goes out: an SMTP server, or a pickup folder when <see cref="PickupDirectory"/> is set.</summary>
public sealed record MailTransportSettings(
    string? Host,
    int Port,
    string Security,
    string? Username,
    string? Password,
    string FromAddress,
    string? FromName,
    string? PickupDirectory = null)
{
    public static MailTransportSettings From(MailDeliverySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new(settings.Host, settings.Port, settings.Security, settings.Username, settings.Password, settings.FromAddress, settings.FromName);
    }
}

/// <summary>Talks to the mail server. The test email uses it with settings that are not saved yet.</summary>
public interface IMailTransport
{
    /// <summary>Delivers the message; throws when delivery fails.</summary>
    Task SendAsync(MailTransportSettings settings, EmailMessage message, CancellationToken cancellationToken);
}

/// <summary>Delivers email right now with the settings in force. Tests replace it.</summary>
public interface IEmailSender
{
    /// <summary>Whether delivery is configured at all; features that need email are offered only then.</summary>
    Task<bool> IsConfiguredAsync(CancellationToken cancellationToken);

    /// <summary>Delivers the message; throws when delivery fails, so the caller decides what that means.</summary>
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

/// <summary>Sends through MailKit, or writes <c>.eml</c> files to a pickup folder.</summary>
internal sealed partial class MailKitTransport : IMailTransport
{
    private readonly ILogger<MailKitTransport> _logger;

    public MailKitTransport(ILogger<MailKitTransport> logger)
    {
        _logger = logger;
    }

    public async Task SendAsync(MailTransportSettings settings, EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(message);

        using var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(settings.FromName ?? string.Empty, settings.FromAddress));
        mime.To.Add(new MailboxAddress(message.ToName ?? string.Empty, message.To));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder { TextBody = message.TextBody, HtmlBody = message.HtmlBody }.ToMessageBody();

        if (string.IsNullOrWhiteSpace(settings.Host))
        {
            if (string.IsNullOrWhiteSpace(settings.PickupDirectory))
            {
                throw new InvalidOperationException("No SMTP server and no pickup folder are configured.");
            }

            Directory.CreateDirectory(settings.PickupDirectory);
            var path = Path.Combine(settings.PickupDirectory, $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.eml");
            await mime.WriteToAsync(path, cancellationToken).ConfigureAwait(false);
            LogWrittenToPickup(message.Subject, path);
            return;
        }

        using var client = new SmtpClient { Timeout = 30_000 };

        var security = settings.Security.Trim().ToUpperInvariant() switch
        {
            "STARTTLS" => SecureSocketOptions.StartTls,
            "SSL" or "SSLONCONNECT" => SecureSocketOptions.SslOnConnect,
            "NONE" => SecureSocketOptions.None,
            _ => SecureSocketOptions.Auto,
        };

        await client.ConnectAsync(settings.Host, settings.Port, security, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(settings.Username))
        {
            await client.AuthenticateAsync(settings.Username, settings.Password ?? string.Empty, cancellationToken).ConfigureAwait(false);
        }

        await client.SendAsync(mime, cancellationToken).ConfigureAwait(false);
        await client.DisconnectAsync(quit: true, cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Email \"{Subject}\" written to {Path}.")]
    private partial void LogWrittenToPickup(string subject, string path);
}

/// <summary>
/// Sends with the SMTP server saved in the portal when there is one, else with the configuration file's server or
/// pickup folder, else reports itself unconfigured.
/// </summary>
internal sealed class ConfiguredEmailSender : IEmailSender
{
    private readonly IMailSettingsReader _stored;
    private readonly IMailTransport _transport;
    private readonly EmailOptions _options;

    public ConfiguredEmailSender(IMailSettingsReader stored, IMailTransport transport, Microsoft.Extensions.Options.IOptions<EmailOptions> options)
    {
        _stored = stored;
        _transport = transport;
        _options = options.Value;
    }

    public async Task<bool> IsConfiguredAsync(CancellationToken cancellationToken) =>
        await ResolveAsync(cancellationToken).ConfigureAwait(false) is not null;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var settings = await ResolveAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Email delivery is not configured (Settings → Email, or Email:From with Email:Smtp:Host or Email:PickupDirectory).");

        await _transport.SendAsync(settings, message, cancellationToken).ConfigureAwait(false);
    }

    private async Task<MailTransportSettings?> ResolveAsync(CancellationToken cancellationToken)
    {
        if (await _stored.GetAsync(cancellationToken).ConfigureAwait(false) is { } stored)
        {
            return MailTransportSettings.From(stored);
        }

        if (!_options.IsConfigured)
        {
            return null;
        }

        return _options.UsesSmtp
            ? new MailTransportSettings(
                _options.Smtp.Host,
                _options.Smtp.Port,
                _options.Smtp.Security,
                _options.Smtp.Username,
                _options.Smtp.Password,
                _options.From!,
                _options.FromName)
            : new MailTransportSettings(null, 0, "none", null, null, _options.From!, _options.FromName, _options.PickupDirectory);
    }
}

/// <summary>Tells the Notifications module whether the configuration file can send email on its own.</summary>
internal sealed class ConfigurationMailFallback : IMailFallbackSource
{
    private readonly EmailOptions _options;

    public ConfigurationMailFallback(Microsoft.Extensions.Options.IOptions<EmailOptions> options)
    {
        _options = options.Value;
    }

    public bool IsConfigured => _options.IsConfigured;
}

/// <summary>Delivers the queued emails (outbox type <c>email</c>).</summary>
internal sealed class EmailOutboxHandler : IOutboxHandler
{
    public const string MessageType = "email";

    private readonly IEmailSender _sender;

    public EmailOutboxHandler(IEmailSender sender)
    {
        _sender = sender;
    }

    public string Type => MessageType;

    public async Task HandleAsync(string payload, CancellationToken cancellationToken)
    {
        var message = JsonSerializer.Deserialize<EmailMessage>(payload)
            ?? throw new OutboxPermanentFailureException("The queued email is empty.");

        if (!await _sender.IsConfiguredAsync(cancellationToken).ConfigureAwait(false))
        {
            // Not permanent: an administrator may configure email, and the message goes out then.
            throw new InvalidOperationException("Email delivery is not configured.");
        }

        await _sender.SendAsync(message, cancellationToken).ConfigureAwait(false);
    }

    public static OutboxMessageDraft Draft(EmailMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return new OutboxMessageDraft(MessageType, JsonSerializer.Serialize(message), $"{message.To}: {message.Subject}");
    }
}

public static class EmailSetup
{
    public static IServiceCollection AddOpenPortalEmail(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<EmailOptions>().Bind(configuration.GetSection(EmailOptions.SectionName));
        services.AddSingleton<IMailTransport, MailKitTransport>();
        services.AddScoped<IEmailSender, ConfiguredEmailSender>();
        services.AddSingleton<IMailFallbackSource, ConfigurationMailFallback>();
        services.AddScoped<IOutboxHandler, EmailOutboxHandler>();

        return services;
    }
}
