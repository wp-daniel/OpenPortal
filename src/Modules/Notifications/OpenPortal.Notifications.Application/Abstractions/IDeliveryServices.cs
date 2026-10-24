using OpenPortal.Notifications.Application.Contracts;
using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Notifications.Application.Abstractions;

/// <summary>
/// The SMTP server the portal sends email through, edited from the portal (administrators only, checked through
/// <see cref="IDeliveryAdministrationAuthorization"/>).
/// </summary>
public interface IMailSettingsService
{
    Task<Result<MailSettingsDto>> GetAsync(CancellationToken cancellationToken);

    Task<Result<MailSettingsDto>> UpdateAsync(UpdateMailSettingsRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Resolves the settings a test email should use: the request's values, with the stored password when the
    /// request leaves it empty. Checks the caller like the other operations.
    /// </summary>
    Task<Result<MailDeliverySettings>> ResolveForTestAsync(TestMailSettingsRequest request, CancellationToken cancellationToken);

    /// <summary>Records the outcome of a test email in the audit log.</summary>
    Task RecordTestAsync(string recipient, bool delivered, CancellationToken cancellationToken);
}

/// <summary>
/// The stored email settings with the password in clear, for the host's mail transport. Performs no caller
/// check, so it must never be put behind an endpoint. Cached briefly; null while the portal has no SMTP server of
/// its own.
/// </summary>
public interface IMailSettingsReader
{
    Task<MailDeliverySettings?> GetAsync(CancellationToken cancellationToken);
}

/// <summary>The delivery queue as administrators see it: what is waiting, what failed and why, and a retry.</summary>
public interface IOutboxAdministration
{
    Task<Result<OutboxPageDto>> ListAsync(OutboxQuery query, CancellationToken cancellationToken);

    Task<Result<OutboxMessageDto>> RetryAsync(Guid messageId, CancellationToken cancellationToken);

    /// <summary>Puts every failed message back in the queue and returns how many.</summary>
    Task<Result<int>> RetryFailedAsync(CancellationToken cancellationToken);

    Task<Result> DeleteAsync(Guid messageId, CancellationToken cancellationToken);
}

/// <summary>
/// Whether the host's configuration file names an SMTP server or a pickup folder, used while the portal has no
/// server of its own. Supplied by the host.
/// </summary>
public interface IMailFallbackSource
{
    bool IsConfigured { get; }
}

/// <summary>
/// The delivery queue's state for health checks and metrics. Performs no caller check and reveals no message, only
/// counts, so it must still not be put behind an endpoint of its own.
/// </summary>
public interface IOutboxMonitor
{
    Task<OutboxSummaryDto> GetSummaryAsync(CancellationToken cancellationToken);
}

/// <summary>Decides whether the caller may manage email delivery. Supplied by the host: administrators only.</summary>
public interface IDeliveryAdministrationAuthorization
{
    Task<Result> EnsureCanManageAsync(CancellationToken cancellationToken);
}
