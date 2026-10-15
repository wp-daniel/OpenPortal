using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Notifications.Application.Abstractions;
using OpenPortal.Notifications.Application.Contracts;
using OpenPortal.Web.Authorization;
using OpenPortal.Web.Infrastructure;
using OpenPortal.Web.Localization;

namespace OpenPortal.Web.Controllers;

/// <summary>The banners shown across the portal, in the reader's language. Open to everyone: the sign-in page shows them.</summary>
[ApiController]
[Route("api/announcements")]
[AllowAnonymous]
[Produces("application/json")]
public sealed class AnnouncementsController : ControllerBase
{
    private readonly IAnnouncementService _announcements;
    private readonly ICurrentUser _currentUser;
    private readonly LanguageCatalog _languages;

    public AnnouncementsController(IAnnouncementService announcements, ICurrentUser currentUser, LanguageCatalog languages)
    {
        _announcements = announcements;
        _currentUser = currentUser;
        _languages = languages;
    }

    /// <summary>
    /// The announcements showing now, most important first. Anonymous callers see only those meant for everyone;
    /// the language is the request's (<c>?lang=</c> or <c>Accept-Language</c>).
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ActiveAnnouncementDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<ActiveAnnouncementDto>> ActiveAsync(CancellationToken cancellationToken) =>
        await _announcements
            .ActiveAsync(
                CultureInfo.CurrentUICulture.Name,
                _languages.DefaultLanguage,
                _currentUser.IsAuthenticated,
                cancellationToken)
            .ConfigureAwait(false);
}

/// <summary>Writing the announcements, for administrators and holders of the announcements page.</summary>
[ApiController]
[Route("api/admin/announcements")]
[Authorize]
[Produces("application/json")]
public sealed class AdminAnnouncementsController : ControllerBase
{
    private readonly IAnnouncementService _announcements;

    public AdminAnnouncementsController(IAnnouncementService announcements)
    {
        _announcements = announcements;
    }

    /// <summary>Every announcement, scheduled, showing or ended, newest first.</summary>
    [RequirePortalPage(PortalPages.Announcements)]
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AnnouncementDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<AnnouncementDto>>> ListAsync(CancellationToken cancellationToken)
    {
        var announcements = await _announcements.ListAsync(cancellationToken).ConfigureAwait(false);

        return announcements.IsSuccess ? Ok(announcements.Value) : ProblemResults.FromResult(HttpContext, announcements);
    }

    [RequirePortalPage(PortalPages.Announcements)]
    [HttpPost]
    [ProducesResponseType<AnnouncementDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AnnouncementDto>> CreateAsync(
        [FromBody] SaveAnnouncementRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _announcements.CreateAsync(request, cancellationToken).ConfigureAwait(false);

        return created.IsSuccess ? Ok(created.Value) : ProblemResults.FromResult(HttpContext, created);
    }

    [RequirePortalPage(PortalPages.Announcements)]
    [HttpPut("{announcementId:guid}")]
    [ProducesResponseType<AnnouncementDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AnnouncementDto>> UpdateAsync(
        Guid announcementId,
        [FromBody] SaveAnnouncementRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _announcements.UpdateAsync(announcementId, request, cancellationToken).ConfigureAwait(false);

        return updated.IsSuccess ? Ok(updated.Value) : ProblemResults.FromResult(HttpContext, updated);
    }

    [RequirePortalPage(PortalPages.Announcements)]
    [HttpDelete("{announcementId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(Guid announcementId, CancellationToken cancellationToken)
    {
        var deleted = await _announcements.DeleteAsync(announcementId, cancellationToken).ConfigureAwait(false);

        return deleted.IsSuccess ? NoContent() : ProblemResults.FromResult(HttpContext, deleted);
    }
}
