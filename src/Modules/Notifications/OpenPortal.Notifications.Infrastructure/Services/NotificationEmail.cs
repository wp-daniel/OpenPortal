using OpenPortal.SharedKernel.Jobs;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenPortal.Notifications.Application.Abstractions;
using OpenPortal.Notifications.Application.Contracts;
using OpenPortal.Notifications.Domain;
using OpenPortal.Notifications.Infrastructure.Configuration;
using OpenPortal.Notifications.Infrastructure.Persistence;
using OpenPortal.SharedKernel.Results;
using OpenPortal.SharedKernel.Time;

namespace OpenPortal.Notifications.Infrastructure.Services;

/// <inheritdoc />
internal sealed class NotificationPreferenceService : INotificationPreferenceService
{
    private readonly NotificationsDbContext _db;
    private readonly INotificationContext _context;
    private readonly INotificationMailer _mailer;
    private readonly IClock _clock;

    public NotificationPreferenceService(NotificationsDbContext db, INotificationContext context, INotificationMailer mailer, IClock clock)
    {
        _db = db;
        _context = context;
        _mailer = mailer;
        _clock = clock;
    }

    public async Task<Result<NotificationPreferencesDto>> GetMineAsync(CancellationToken cancellationToken)
    {
        if (_context.CurrentUserId is not Guid userId)
        {
            return Result<NotificationPreferencesDto>.Failure(NotificationErrors.SignInRequired);
        }

        var preference = await _db.NotificationPreferences.AsNoTracking()
            .FirstOrDefaultAsync(row => row.UserId == userId, cancellationToken)
            .ConfigureAwait(false);

        return Result<NotificationPreferencesDto>.Success(await ToDtoAsync(preference?.Email ?? EmailDelivery.SecurityOnly, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<NotificationPreferencesDto>> UpdateMineAsync(UpdateNotificationPreferencesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_context.CurrentUserId is not Guid userId)
        {
            return Result<NotificationPreferencesDto>.Failure(NotificationErrors.SignInRequired);
        }

        if (EmailDeliveries.FromContract(request.Email) is not { } email)
        {
            return Result<NotificationPreferencesDto>.Failure(NotificationErrors.EmailDeliveryInvalid);
        }

        var preference = await _db.NotificationPreferences
            .FirstOrDefaultAsync(row => row.UserId == userId, cancellationToken)
            .ConfigureAwait(false);

        if (preference is null)
        {
            _db.NotificationPreferences.Add(new NotificationPreference(userId, email, _clock.UtcNow));
        }
        else
        {
            preference.Change(email, _clock.UtcNow);
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<NotificationPreferencesDto>.Success(await ToDtoAsync(email, cancellationToken).ConfigureAwait(false));
    }

    private async Task<NotificationPreferencesDto> ToDtoAsync(EmailDelivery email, CancellationToken cancellationToken) =>
        new(EmailDeliveries.ToContract(email), await _mailer.CanSendAsync(cancellationToken).ConfigureAwait(false));
}

/// <summary>Emails a stored notice to the recipients whose preference says so. Best effort, like the notice itself.</summary>
internal sealed partial class NotificationEmailer
{
    private readonly NotificationsDbContext _db;
    private readonly INotificationMailer _mailer;
    private readonly ILogger<NotificationEmailer> _logger;

    public NotificationEmailer(NotificationsDbContext db, INotificationMailer mailer, ILogger<NotificationEmailer> logger)
    {
        _db = db;
        _mailer = mailer;
        _logger = logger;
    }

    public async Task SendAsync(
        IReadOnlyCollection<Guid> recipients,
        NotificationMailItem item,
        bool security,
        CancellationToken cancellationToken)
    {
        if (recipients.Count == 0 || !await _mailer.CanSendAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var ids = recipients.ToList();
        var chosen = await _db.NotificationPreferences.AsNoTracking()
            .Where(row => ids.Contains(row.UserId))
            .ToDictionaryAsync(row => row.UserId, row => row.Email, cancellationToken)
            .ConfigureAwait(false);

        foreach (var recipient in ids)
        {
            var email = chosen.TryGetValue(recipient, out var value) ? value : EmailDelivery.SecurityOnly;
            if (!NotificationPreference.EmailsAtOnce(email, security))
            {
                continue;
            }

            try
            {
                await _mailer.SendAsync(recipient, [item], digest: false, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogMailFailed(exception, item.Type);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The notification {Type} could not be emailed.")]
    private partial void LogMailFailed(Exception exception, string type);
}

/// <summary>
/// Sends the daily digest: for everyone who chose it, one email with the notifications that arrived since the last
/// one and are still unread. Several instances may run it: a digest is claimed by moving its date first, so only one
/// instance sends it.
/// </summary>
internal sealed partial class NotificationDigestService : BackgroundService
{
    /// <summary>The job's name in the job list (Administration → System).</summary>
    public const string JobName = "notifications.digest";

    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    private readonly IJobRegistry _jobs;

    private readonly IServiceScopeFactory _scopes;
    private readonly IClock _clock;
    private readonly IOptions<NotificationsModuleOptions> _options;
    private readonly ILogger<NotificationDigestService> _logger;

    public NotificationDigestService(
        IServiceScopeFactory scopes,
        IClock clock,
        IOptions<NotificationsModuleOptions> options,
        ILogger<NotificationDigestService> logger,
        IJobRegistry jobs)
    {
        _jobs = jobs;
        jobs.Register(JobName, Interval, SendDueAsync);
        _scopes = scopes;
        _clock = clock;
        _options = options;
        _logger = logger;
    }

    /// <summary>Sends the digests that are due and returns how many went.</summary>
    public async Task<int> SendDueAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        var mailer = scope.ServiceProvider.GetRequiredService<INotificationMailer>();

        if (!await mailer.CanSendAsync(cancellationToken).ConfigureAwait(false))
        {
            return 0;
        }

        var now = _clock.UtcNow;
        var due = now.AddHours(-Math.Clamp(_options.Value.DigestIntervalHours, 1, 168));

        var candidates = await db.NotificationPreferences.AsNoTracking()
            .Where(row => row.Email == EmailDelivery.DailyDigest && (row.LastDigestAtUtc == null || row.LastDigestAtUtc <= due))
            .Select(row => new { row.UserId, row.LastDigestAtUtc })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var sent = 0;
        foreach (var candidate in candidates)
        {
            // Claim it: an instance that moved the date first sends it; the others find nothing to update.
            var claimed = await db.NotificationPreferences
                .Where(row => row.UserId == candidate.UserId && row.LastDigestAtUtc == candidate.LastDigestAtUtc)
                .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.LastDigestAtUtc, now), cancellationToken)
                .ConfigureAwait(false);
            if (claimed == 0)
            {
                continue;
            }

            var since = candidate.LastDigestAtUtc ?? due;
            var unread = await db.Notifications.AsNoTracking()
                .Where(row => row.RecipientId == candidate.UserId && row.ReadAtUtc == null && row.CreatedAtUtc > since)
                .OrderByDescending(row => row.CreatedAtUtc)
                .Take(50)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (unread.Count == 0)
            {
                continue;
            }

            await mailer.SendAsync(
                    candidate.UserId,
                    unread.Select(row => new NotificationMailItem(row.Type, Parameters(row.Parameters), row.Link, row.CreatedAtUtc)).ToList(),
                    digest: true,
                    cancellationToken)
                .ConfigureAwait(false);
            sent++;
        }

        return sent;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var timer = new PeriodicTimer(Interval);

            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                try
                {
                    var sent = await _jobs.RunAsync(JobName, stoppingToken).ConfigureAwait(false);
                    if (sent > 0)
                    {
                        LogSent(sent);
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    LogFailed(exception);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
    }

    internal static IReadOnlyDictionary<string, string?> Parameters(string? json) =>
        string.IsNullOrEmpty(json)
            ? new Dictionary<string, string?>()
            : JsonSerializer.Deserialize<Dictionary<string, string?>>(json) ?? new Dictionary<string, string?>();

    [LoggerMessage(Level = LogLevel.Information, Message = "Sent {Count} notification digests.")]
    private partial void LogSent(int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The notification digests could not be sent.")]
    private partial void LogFailed(Exception exception);
}
