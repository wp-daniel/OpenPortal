using Microsoft.EntityFrameworkCore;
using OpenPortal.Notifications.Application.Abstractions;
using OpenPortal.Notifications.Application.Auditing;
using OpenPortal.Notifications.Application.Contracts;
using OpenPortal.Notifications.Domain;
using OpenPortal.Notifications.Infrastructure.Persistence;
using OpenPortal.SharedKernel.Auditing;
using OpenPortal.SharedKernel.Results;
using OpenPortal.SharedKernel.Time;

namespace OpenPortal.Notifications.Infrastructure.Services;

/// <summary>The portal's banners. See <see cref="IAnnouncementService"/>.</summary>
internal sealed class AnnouncementService : IAnnouncementService
{
    private readonly NotificationsDbContext _db;
    private readonly IAnnouncementAuthorization _authorization;
    private readonly IClock _clock;
    private readonly IAuditTrail _audit;

    public AnnouncementService(
        NotificationsDbContext db,
        IAnnouncementAuthorization authorization,
        IClock clock,
        IAuditTrail audit)
    {
        _db = db;
        _authorization = authorization;
        _clock = clock;
        _audit = audit;
    }

    public async Task<IReadOnlyList<ActiveAnnouncementDto>> ActiveAsync(
        string? language,
        string? defaultLanguage,
        bool signedIn,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        // A handful of rows at most, so the window is checked in memory: it also spares SQLite ordering
        // DateTimeOffset values.
        var announcements = await _db.Announcements
            .AsNoTracking()
            .Where(announcement => signedIn || announcement.Audience == AnnouncementAudience.Everyone)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return announcements
            .Where(announcement => announcement.IsActiveAt(now) && announcement.Texts.Count > 0)
            .OrderByDescending(announcement => announcement.Severity)
            .ThenByDescending(announcement => announcement.StartsAtUtc ?? announcement.CreatedAtUtc)
            .Select(announcement =>
            {
                var text = announcement.TextFor(language, defaultLanguage);

                return new ActiveAnnouncementDto(
                    announcement.Id,
                    SeverityName(announcement.Severity),
                    text.Title,
                    text.Body,
                    announcement.Dismissible,
                    announcement.EndsAtUtc,
                    announcement.UpdatedAtUtc ?? announcement.CreatedAtUtc);
            })
            .ToList();
    }

    public async Task<Result<IReadOnlyList<AnnouncementDto>>> ListAsync(CancellationToken cancellationToken)
    {
        var guard = await _authorization.EnsureCanManageAsync(cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<IReadOnlyList<AnnouncementDto>>.Failure(guard.Error);
        }

        var announcements = await _db.Announcements.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        var now = _clock.UtcNow;

        return Result<IReadOnlyList<AnnouncementDto>>.Success(announcements
            .OrderByDescending(announcement => announcement.CreatedAtUtc)
            .Select(announcement => ToDto(announcement, now))
            .ToList());
    }

    public async Task<Result<AnnouncementDto>> CreateAsync(SaveAnnouncementRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var guard = await _authorization.EnsureCanManageAsync(cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<AnnouncementDto>.Failure(guard.Error);
        }

        var content = ToContent(request);
        if (content.IsFailure)
        {
            return Result<AnnouncementDto>.Failure(content.Error);
        }

        var now = _clock.UtcNow;
        var created = Announcement.Create(Guid.NewGuid(), content.Value, now);
        if (created.IsFailure)
        {
            return Result<AnnouncementDto>.Failure(created.Error);
        }

        _db.Announcements.Add(created.Value);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await RecordAsync(NotificationAuditActions.AnnouncementCreated, created.Value, cancellationToken).ConfigureAwait(false);

        return Result<AnnouncementDto>.Success(ToDto(created.Value, now));
    }

    public async Task<Result<AnnouncementDto>> UpdateAsync(
        Guid announcementId,
        SaveAnnouncementRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var guard = await _authorization.EnsureCanManageAsync(cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return Result<AnnouncementDto>.Failure(guard.Error);
        }

        var announcement = await _db.Announcements
            .FirstOrDefaultAsync(candidate => candidate.Id == announcementId, cancellationToken)
            .ConfigureAwait(false);
        if (announcement is null)
        {
            return Result<AnnouncementDto>.Failure(AnnouncementErrors.NotFound);
        }

        var content = ToContent(request);
        if (content.IsFailure)
        {
            return Result<AnnouncementDto>.Failure(content.Error);
        }

        var now = _clock.UtcNow;
        var updated = announcement.Update(content.Value, now);
        if (updated.IsFailure)
        {
            return Result<AnnouncementDto>.Failure(updated.Error);
        }

        if (updated.Value)
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await RecordAsync(NotificationAuditActions.AnnouncementUpdated, announcement, cancellationToken).ConfigureAwait(false);
        }

        return Result<AnnouncementDto>.Success(ToDto(announcement, now));
    }

    public async Task<Result> DeleteAsync(Guid announcementId, CancellationToken cancellationToken)
    {
        var guard = await _authorization.EnsureCanManageAsync(cancellationToken).ConfigureAwait(false);
        if (guard.IsFailure)
        {
            return guard;
        }

        var announcement = await _db.Announcements
            .FirstOrDefaultAsync(candidate => candidate.Id == announcementId, cancellationToken)
            .ConfigureAwait(false);
        if (announcement is null)
        {
            return Result.Failure(AnnouncementErrors.NotFound);
        }

        _db.Announcements.Remove(announcement);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await RecordAsync(NotificationAuditActions.AnnouncementDeleted, announcement, cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    private Task RecordAsync(string action, Announcement announcement, CancellationToken cancellationToken) =>
        _audit.RecordAsync(
            AuditEvent.Succeeded(
                action,
                new AuditSubject(AuditSubjectTypes.Announcement, announcement.Id.ToString(), announcement.Texts.FirstOrDefault()?.Title),
                new Dictionary<string, string?> { ["severity"] = SeverityName(announcement.Severity) }),
            cancellationToken);

    private static Result<AnnouncementContent> ToContent(SaveAnnouncementRequest request)
    {
        AnnouncementSeverity? severity = request.Severity?.Trim().ToLowerInvariant() switch
        {
            AnnouncementNames.Info => AnnouncementSeverity.Info,
            AnnouncementNames.Warning => AnnouncementSeverity.Warning,
            AnnouncementNames.Maintenance => AnnouncementSeverity.Maintenance,
            _ => null,
        };

        AnnouncementAudience? audience = request.Audience?.Trim().ToLowerInvariant() switch
        {
            AnnouncementNames.Everyone => AnnouncementAudience.Everyone,
            AnnouncementNames.SignedIn => AnnouncementAudience.SignedIn,
            _ => null,
        };

        if (severity is null || audience is null)
        {
            return Result<AnnouncementContent>.Failure(AnnouncementErrors.Invalid);
        }

        return Result<AnnouncementContent>.Success(new AnnouncementContent(
            severity.Value,
            audience.Value,
            request.StartsAtUtc,
            request.EndsAtUtc,
            request.Dismissible,
            (request.Texts ?? []).Select(text => new AnnouncementTextContent(text?.Language, text?.Title, text?.Body)).ToList()));
    }

    private static AnnouncementDto ToDto(Announcement announcement, DateTimeOffset now) => new(
        announcement.Id,
        SeverityName(announcement.Severity),
        announcement.Audience == AnnouncementAudience.Everyone ? AnnouncementNames.Everyone : AnnouncementNames.SignedIn,
        announcement.StartsAtUtc,
        announcement.EndsAtUtc,
        announcement.Dismissible,
        announcement.Texts.Select(text => new AnnouncementTextDto(text.Language, text.Title, text.Body)).ToList(),
        announcement.StartsAtUtc > now
            ? AnnouncementNames.Scheduled
            : announcement.EndsAtUtc <= now ? AnnouncementNames.Ended : AnnouncementNames.Active,
        announcement.CreatedAtUtc,
        announcement.UpdatedAtUtc);

    private static string SeverityName(AnnouncementSeverity severity) => severity switch
    {
        AnnouncementSeverity.Warning => AnnouncementNames.Warning,
        AnnouncementSeverity.Maintenance => AnnouncementNames.Maintenance,
        _ => AnnouncementNames.Info,
    };
}
