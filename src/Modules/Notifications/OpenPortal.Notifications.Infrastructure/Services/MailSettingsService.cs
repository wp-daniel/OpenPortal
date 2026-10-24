using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenPortal.Notifications.Application.Abstractions;
using OpenPortal.Notifications.Application.Auditing;
using OpenPortal.Notifications.Application.Contracts;
using OpenPortal.Notifications.Domain;
using OpenPortal.Notifications.Infrastructure.Persistence;
using OpenPortal.SharedKernel.Auditing;
using OpenPortal.SharedKernel.Results;
using OpenPortal.SharedKernel.Time;

namespace OpenPortal.Notifications.Infrastructure.Services;

/// <summary>
/// Encrypts the SMTP password with the portal's Data Protection keys (the same ones that protect the session
/// cookie), so a copy of the database alone does not reveal it.
/// </summary>
internal sealed class MailPasswordProtector
{
    private const string Purpose = "OpenPortal.Notifications.MailSettings.Password.v1";

    private readonly IDataProtector _protector;

    public MailPasswordProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    public string Protect(string password) => _protector.Protect(password);

    /// <summary>The password, or null when it cannot be decrypted (the keys were replaced).</summary>
    public string? TryUnprotect(string? protectedPassword)
    {
        if (string.IsNullOrEmpty(protectedPassword))
        {
            return null;
        }

        try
        {
            return _protector.Unprotect(protectedPassword);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}

/// <summary>The stored settings, kept for a short while so sending an email does not read the row every time.</summary>
internal sealed class MailSettingsCache
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);

    private readonly Lock _gate = new();
    private (MailDeliverySettings? Value, DateTimeOffset ExpiresAt)? _entry;

    public bool TryGet(DateTimeOffset now, out MailDeliverySettings? value)
    {
        lock (_gate)
        {
            if (_entry is { } entry && entry.ExpiresAt > now)
            {
                value = entry.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    public void Set(MailDeliverySettings? value, DateTimeOffset now)
    {
        lock (_gate)
        {
            _entry = (value, now + Lifetime);
        }
    }

    /// <summary>Called after a change on this instance; other instances see it within the lifetime above.</summary>
    public void Invalidate()
    {
        lock (_gate)
        {
            _entry = null;
        }
    }
}

internal static class MailSecurityNames
{
    public static string ToName(MailSecurity security) => security switch
    {
        MailSecurity.StartTls => "starttls",
        MailSecurity.SslOnConnect => "ssl",
        MailSecurity.None => "none",
        _ => "auto",
    };

    public static bool TryParse(string? value, out MailSecurity security)
    {
        switch (value?.Trim().ToUpperInvariant())
        {
            case null or "" or "AUTO":
                security = MailSecurity.Auto;
                return true;
            case "STARTTLS":
                security = MailSecurity.StartTls;
                return true;
            case "SSL" or "SSLONCONNECT" or "TLS":
                security = MailSecurity.SslOnConnect;
                return true;
            case "NONE":
                security = MailSecurity.None;
                return true;
            default:
                security = MailSecurity.Auto;
                return false;
        }
    }
}

/// <inheritdoc />
internal sealed partial class MailSettingsReader : IMailSettingsReader
{
    private readonly NotificationsDbContext _db;
    private readonly MailPasswordProtector _protector;
    private readonly MailSettingsCache _cache;
    private readonly IClock _clock;
    private readonly ILogger<MailSettingsReader> _logger;

    public MailSettingsReader(
        NotificationsDbContext db,
        MailPasswordProtector protector,
        MailSettingsCache cache,
        IClock clock,
        ILogger<MailSettingsReader> logger)
    {
        _db = db;
        _protector = protector;
        _cache = cache;
        _clock = clock;
        _logger = logger;
    }

    public async Task<MailDeliverySettings?> GetAsync(CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        if (_cache.TryGet(now, out var cached))
        {
            return cached;
        }

        var settings = await _db.MailSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == MailSettings.SingletonId, cancellationToken)
            .ConfigureAwait(false);

        MailDeliverySettings? value = null;
        if (settings is { IsConfigured: true })
        {
            var password = _protector.TryUnprotect(settings.ProtectedPassword);
            if (password is null && settings.ProtectedPassword is not null)
            {
                LogPasswordUnreadable();
            }

            value = new MailDeliverySettings(
                settings.Host!,
                settings.Port,
                MailSecurityNames.ToName(settings.Security),
                settings.Username,
                password,
                settings.FromAddress!,
                settings.FromName);
        }

        _cache.Set(value, now);

        return value;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The stored SMTP password cannot be decrypted (the Data Protection keys changed); enter it again in the email settings.")]
    private partial void LogPasswordUnreadable();
}

/// <inheritdoc />
internal sealed class MailSettingsService : IMailSettingsService
{
    private readonly NotificationsDbContext _db;
    private readonly IDeliveryAdministrationAuthorization _authorization;
    private readonly MailPasswordProtector _protector;
    private readonly MailSettingsCache _cache;
    private readonly IMailFallbackSource _fallback;
    private readonly IClock _clock;
    private readonly IAuditTrail _audit;

    public MailSettingsService(
        NotificationsDbContext db,
        IDeliveryAdministrationAuthorization authorization,
        MailPasswordProtector protector,
        MailSettingsCache cache,
        IMailFallbackSource fallback,
        IClock clock,
        IAuditTrail audit)
    {
        _db = db;
        _authorization = authorization;
        _protector = protector;
        _cache = cache;
        _fallback = fallback;
        _clock = clock;
        _audit = audit;
    }

    public async Task<Result<MailSettingsDto>> GetAsync(CancellationToken cancellationToken)
    {
        var guard = await _authorization.EnsureCanManageAsync(cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<MailSettingsDto>.Failure(guard.Error);
        }

        var settings = await ReadAsync(cancellationToken).ConfigureAwait(false) ?? MailSettings.Defaults();

        return Result<MailSettingsDto>.Success(ToDto(settings));
    }

    public async Task<Result<MailSettingsDto>> UpdateAsync(UpdateMailSettingsRequest request, CancellationToken cancellationToken)
    {
        var guard = await _authorization.EnsureCanManageAsync(cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<MailSettingsDto>.Failure(guard.Error);
        }

        ArgumentNullException.ThrowIfNull(request);

        if (!MailSecurityNames.TryParse(request.Security, out var security))
        {
            return Result<MailSettingsDto>.Failure(MailSettingsErrors.SecurityInvalid);
        }

        var settings = await _db.MailSettings
            .FirstOrDefaultAsync(row => row.Id == MailSettings.SingletonId, cancellationToken)
            .ConfigureAwait(false);

        var isNew = settings is null;
        settings ??= MailSettings.Defaults();

        var newPassword = string.IsNullOrEmpty(request.Password) ? null : _protector.Protect(request.Password);
        var passwordChanged = newPassword is not null || (request.ClearPassword && settings.ProtectedPassword is not null);

        var changed = settings.Update(
            request.Host,
            request.Port,
            security,
            request.Username,
            newPassword,
            request.ClearPassword,
            request.FromAddress,
            request.FromName,
            _clock.UtcNow);

        if (changed.IsFailure)
        {
            return Result<MailSettingsDto>.Failure(changed.Error);
        }

        if (changed.Value)
        {
            if (isNew)
            {
                _db.MailSettings.Add(settings);
            }

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _cache.Invalidate();

            await _audit.RecordAsync(
                    AuditEvent.Succeeded(
                        NotificationAuditActions.MailSettingsUpdated,
                        details: new Dictionary<string, string?>
                        {
                            ["host"] = settings.Host ?? "-",
                            ["port"] = settings.Port.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            ["security"] = MailSecurityNames.ToName(settings.Security),
                            ["from"] = settings.FromAddress ?? "-",
                            ["password"] = passwordChanged ? "changed" : "unchanged",
                        }),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return Result<MailSettingsDto>.Success(ToDto(settings));
    }

    public async Task<Result<MailDeliverySettings>> ResolveForTestAsync(TestMailSettingsRequest request, CancellationToken cancellationToken)
    {
        var guard = await _authorization.EnsureCanManageAsync(cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<MailDeliverySettings>.Failure(guard.Error);
        }

        ArgumentNullException.ThrowIfNull(request);

        if (!MailSettings.IsAddress(request.To))
        {
            return Result<MailDeliverySettings>.Failure(MailSettingsErrors.RecipientInvalid);
        }

        if (!MailSecurityNames.TryParse(request.Security, out var security))
        {
            return Result<MailDeliverySettings>.Failure(MailSettingsErrors.SecurityInvalid);
        }

        if (string.IsNullOrWhiteSpace(request.Host))
        {
            // Nothing entered: test what the portal would use now (the configuration file's server).
            return Result<MailDeliverySettings>.Failure(MailSettingsErrors.NotConfigured);
        }

        var valid = MailSettings.Validate(request.Host, request.Port, request.FromAddress, request.FromName, request.Username);
        if (valid.IsFailure)
        {
            return Result<MailDeliverySettings>.Failure(valid.Error);
        }

        var password = request.Password;
        if (string.IsNullOrEmpty(password) && !request.ClearPassword)
        {
            var stored = await ReadAsync(cancellationToken).ConfigureAwait(false);
            password = _protector.TryUnprotect(stored?.ProtectedPassword);
        }

        return Result<MailDeliverySettings>.Success(new MailDeliverySettings(
            request.Host.Trim(),
            request.Port,
            MailSecurityNames.ToName(security),
            string.IsNullOrWhiteSpace(request.Username) ? null : request.Username.Trim(),
            string.IsNullOrEmpty(password) ? null : password,
            request.FromAddress!.Trim(),
            string.IsNullOrWhiteSpace(request.FromName) ? null : request.FromName.Trim()));
    }

    public Task RecordTestAsync(string recipient, bool delivered, CancellationToken cancellationToken) =>
        _audit.RecordAsync(
            delivered
                ? AuditEvent.Succeeded(NotificationAuditActions.MailTestSent, details: new Dictionary<string, string?> { ["to"] = recipient })
                : AuditEvent.Failed(NotificationAuditActions.MailTestSent, details: new Dictionary<string, string?> { ["to"] = recipient, ["reason"] = "smtp_error" }),
            cancellationToken);

    private Task<MailSettings?> ReadAsync(CancellationToken cancellationToken) =>
        _db.MailSettings.AsNoTracking().FirstOrDefaultAsync(row => row.Id == MailSettings.SingletonId, cancellationToken);

    private MailSettingsDto ToDto(MailSettings settings)
    {
        var source = settings.IsConfigured ? "portal" : _fallback.IsConfigured ? "configuration" : "none";

        return new MailSettingsDto(
            settings.Host,
            settings.Port,
            MailSecurityNames.ToName(settings.Security),
            settings.Username,
            settings.ProtectedPassword is not null,
            settings.FromAddress,
            settings.FromName,
            source,
            settings.UpdatedAtUtc);
    }
}
