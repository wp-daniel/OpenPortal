using OpenPortal.Notifications.Application.Contracts;
using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Notifications.Application.Abstractions;

/// <summary>
/// The banners shown across the portal. Reading the active ones is open to everyone (the sign-in page shows
/// them); managing them re-checks the caller through <see cref="IAnnouncementAuthorization"/>.
/// </summary>
public interface IAnnouncementService
{
    /// <summary>The announcements showing now for this reader, most important first, in their language.</summary>
    /// <param name="language">The reader's language.</param>
    /// <param name="defaultLanguage">The portal's default language, used when the announcement has no text in theirs.</param>
    /// <param name="signedIn">Whether the reader is signed in; anonymous readers see only <c>everyone</c> announcements.</param>
    Task<IReadOnlyList<ActiveAnnouncementDto>> ActiveAsync(
        string? language,
        string? defaultLanguage,
        bool signedIn,
        CancellationToken cancellationToken);

    /// <summary>Every announcement, scheduled, showing or ended, newest first.</summary>
    Task<Result<IReadOnlyList<AnnouncementDto>>> ListAsync(CancellationToken cancellationToken);

    Task<Result<AnnouncementDto>> CreateAsync(SaveAnnouncementRequest request, CancellationToken cancellationToken);

    Task<Result<AnnouncementDto>> UpdateAsync(Guid announcementId, SaveAnnouncementRequest request, CancellationToken cancellationToken);

    Task<Result> DeleteAsync(Guid announcementId, CancellationToken cancellationToken);
}

/// <summary>Decides whether the caller may manage announcements. Supplied by the host, which owns the pages.</summary>
public interface IAnnouncementAuthorization
{
    Task<Result> EnsureCanManageAsync(CancellationToken cancellationToken);
}
