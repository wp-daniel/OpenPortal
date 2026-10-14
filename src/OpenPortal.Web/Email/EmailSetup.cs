using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace OpenPortal.Web.Email;

/// <summary>Email delivery settings, bound from the <c>Email</c> section.</summary>
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
}

/// <summary>An email ready to go: the texts are already in the recipient's language.</summary>
public sealed record EmailMessage(string To, string? ToName, string Subject, string TextBody, string HtmlBody);

/// <summary>Delivers email. The host's only door to the outside world for mail, so tests replace it.</summary>
public interface IEmailSender
{
    /// <summary>Whether delivery is configured at all; features that need email are offered only then.</summary>
    bool IsConfigured { get; }

    /// <summary>Delivers the message; throws when delivery fails, so the caller decides what that means.</summary>
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

/// <summary>
/// Sends through an SMTP server when one is configured, else writes <c>.eml</c> files to the pickup directory, else
/// reports itself unconfigured.
/// </summary>
internal sealed partial class MailKitEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<MailKitEmailSender> _logger;

    public MailKitEmailSender(Microsoft.Extensions.Options.IOptions<EmailOptions> options, ILogger<MailKitEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    private bool UsesSmtp => !string.IsNullOrWhiteSpace(_options.Smtp.Host);

    private bool UsesPickup => !string.IsNullOrWhiteSpace(_options.PickupDirectory);

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.From) && (UsesSmtp || UsesPickup);

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (!IsConfigured)
        {
            throw new InvalidOperationException("Email delivery is not configured (Email:From and Email:Smtp:Host or Email:PickupDirectory).");
        }

        using var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(_options.FromName, _options.From!));
        mime.To.Add(new MailboxAddress(message.ToName ?? string.Empty, message.To));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder { TextBody = message.TextBody, HtmlBody = message.HtmlBody }.ToMessageBody();

        if (!UsesSmtp)
        {
            Directory.CreateDirectory(_options.PickupDirectory!);
            var path = Path.Combine(_options.PickupDirectory!, $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.eml");
            await mime.WriteToAsync(path, cancellationToken).ConfigureAwait(false);
            LogWrittenToPickup(message.Subject, path);
            return;
        }

        using var client = new SmtpClient();

        var security = _options.Smtp.Security.Trim().ToUpperInvariant() switch
        {
            "STARTTLS" => SecureSocketOptions.StartTls,
            "SSLONCONNECT" => SecureSocketOptions.SslOnConnect,
            "NONE" => SecureSocketOptions.None,
            _ => SecureSocketOptions.Auto,
        };

        await client.ConnectAsync(_options.Smtp.Host!, _options.Smtp.Port, security, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(_options.Smtp.Username))
        {
            await client.AuthenticateAsync(_options.Smtp.Username, _options.Smtp.Password ?? string.Empty, cancellationToken).ConfigureAwait(false);
        }

        await client.SendAsync(mime, cancellationToken).ConfigureAwait(false);
        await client.DisconnectAsync(quit: true, cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Email \"{Subject}\" written to {Path}.")]
    private partial void LogWrittenToPickup(string subject, string path);
}

public static class EmailSetup
{
    public static IServiceCollection AddOpenPortalEmail(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<EmailOptions>().Bind(configuration.GetSection(EmailOptions.SectionName));
        services.AddSingleton<IEmailSender, MailKitEmailSender>();

        return services;
    }
}
