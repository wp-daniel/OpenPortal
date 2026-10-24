using System.Net.Mail;
using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Notifications.Domain;

/// <summary>How the connection to the SMTP server is secured.</summary>
public enum MailSecurity
{
    /// <summary>STARTTLS when the server offers it, implicit TLS on port 465.</summary>
    Auto = 0,
    StartTls = 1,
    SslOnConnect = 2,
    None = 3,
}

/// <summary>
/// The SMTP server the portal sends email through, edited by administrators from the portal. A single row; while
/// it has no host the portal falls back to the <c>Email</c> section of its configuration.
/// <para>
/// The password is stored only in protected (encrypted) form: the domain never sees it in clear and the API never
/// returns it, only whether one is set.
/// </para>
/// </summary>
public sealed class MailSettings
{
    public const int SingletonId = 1;
    public const int HostMaxLength = 255;
    public const int UsernameMaxLength = 255;
    public const int AddressMaxLength = 254;
    public const int FromNameMaxLength = 128;
    public const int ProtectedPasswordMaxLength = 4000;

    private MailSettings()
    {
    }

    public int Id { get; private set; } = SingletonId;

    /// <summary>The SMTP server; null means "use the configuration file".</summary>
    public string? Host { get; private set; }

    public int Port { get; private set; } = 587;

    public MailSecurity Security { get; private set; } = MailSecurity.Auto;

    public string? Username { get; private set; }

    /// <summary>The password as encrypted by the infrastructure; null when the server needs none.</summary>
    public string? ProtectedPassword { get; private set; }

    public string? FromAddress { get; private set; }

    public string? FromName { get; private set; }

    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public bool IsConfigured => !string.IsNullOrEmpty(Host) && !string.IsNullOrEmpty(FromAddress);

    public static MailSettings Defaults() => new();

    /// <summary>Checks the values without applying them (the connection test uses them unsaved).</summary>
    public static Result Validate(string? host, int port, string? fromAddress, string? fromName, string? username)
    {
        var trimmedHost = host?.Trim();

        if (string.IsNullOrEmpty(trimmedHost))
        {
            // No host: the portal uses its configuration file and the other values do not matter.
            return Result.Success();
        }

        if (trimmedHost.Length > HostMaxLength || trimmedHost.Any(char.IsWhiteSpace) || trimmedHost.Contains("://", StringComparison.Ordinal))
        {
            return Result.Failure(MailSettingsErrors.HostInvalid);
        }

        if (port is < 1 or > 65535)
        {
            return Result.Failure(MailSettingsErrors.PortInvalid);
        }

        if (!IsAddress(fromAddress))
        {
            return Result.Failure(MailSettingsErrors.FromInvalid);
        }

        if (fromName?.Trim().Length > FromNameMaxLength || username?.Trim().Length > UsernameMaxLength)
        {
            return Result.Failure(MailSettingsErrors.TextTooLong);
        }

        return Result.Success();
    }

    /// <summary>
    /// Applies new values; the result says whether anything changed.
    /// </summary>
    /// <param name="protectedPassword">
    /// The new protected password, or null to keep the current one; see <paramref name="clearPassword"/>.
    /// </param>
    /// <param name="clearPassword">Removes the stored password (a server without authentication).</param>
    public Result<bool> Update(
        string? host,
        int port,
        MailSecurity security,
        string? username,
        string? protectedPassword,
        bool clearPassword,
        string? fromAddress,
        string? fromName,
        DateTimeOffset now)
    {
        var valid = Validate(host, port, fromAddress, fromName, username);
        if (valid.IsFailure)
        {
            return Result<bool>.Failure(valid.Error);
        }

        if (protectedPassword?.Length > ProtectedPasswordMaxLength)
        {
            return Result<bool>.Failure(MailSettingsErrors.TextTooLong);
        }

        var newHost = Normalise(host);
        var newUsername = Normalise(username);
        var newFrom = Normalise(fromAddress);
        var newFromName = Normalise(fromName);
        var newPassword = clearPassword ? null : protectedPassword ?? ProtectedPassword;

        if (string.Equals(Host, newHost, StringComparison.Ordinal)
            && Port == port
            && Security == security
            && string.Equals(Username, newUsername, StringComparison.Ordinal)
            && string.Equals(ProtectedPassword, newPassword, StringComparison.Ordinal)
            && string.Equals(FromAddress, newFrom, StringComparison.Ordinal)
            && string.Equals(FromName, newFromName, StringComparison.Ordinal))
        {
            return Result<bool>.Success(false);
        }

        Host = newHost;
        Port = port;
        Security = security;
        Username = newUsername;
        ProtectedPassword = newPassword;
        FromAddress = newFrom;
        FromName = newFromName;
        UpdatedAtUtc = now;

        return Result<bool>.Success(true);
    }

    public static bool IsAddress(string? value)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > AddressMaxLength)
        {
            return false;
        }

        return MailAddress.TryCreate(trimmed, out var parsed)
            && string.Equals(parsed.Address, trimmed, StringComparison.OrdinalIgnoreCase);
    }

    private static string? Normalise(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}

/// <summary>Stable, machine-readable failure codes for the email settings.</summary>
public static class MailSettingsErrors
{
    private const string Prefix = "mail.";

    public static Error HostInvalid { get; } =
        Error.Validation(Prefix + "host_invalid", "The SMTP server must be a host name or an address, without a scheme.");

    public static Error PortInvalid { get; } =
        Error.Validation(Prefix + "port_invalid", "The port must be between 1 and 65535.");

    public static Error FromInvalid { get; } =
        Error.Validation(Prefix + "from_invalid", "The sender must be a valid email address.");

    public static Error SecurityInvalid { get; } =
        Error.Validation(Prefix + "security_invalid", "The connection security must be auto, starttls, ssl or none.");

    public static Error TextTooLong { get; } =
        Error.Validation(Prefix + "text_too_long", "A value of the email settings is too long.");

    public static Error RecipientInvalid { get; } =
        Error.Validation(Prefix + "recipient_invalid", "The test email needs a valid recipient address.");

    public static Error NotConfigured { get; } =
        Error.Conflict(Prefix + "not_configured", "Email delivery is not configured.");

    public static Error TestFailed { get; } =
        Error.Failure(Prefix + "test_failed", "The test email could not be sent.");
}
