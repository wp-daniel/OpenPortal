using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenPortal.Content.Application.Abstractions;
using OpenPortal.Content.Application.Contracts;
using OpenPortal.Web.Authorization;
using OpenPortal.Web.Infrastructure;

namespace OpenPortal.Web.Controllers;

/// <summary>Editing of the site's content, for administrators and holders of the content pages.</summary>
/// <para>
/// The policy is the first gate; the Content service re-checks authorisation through
/// <c>IContentEditAuthorization</c> before mutating anything, so these routes cannot be bypassed.
/// </para>
/// </summary>
[ApiController]
[Route("api/manage/content")]
[Authorize]
[Produces("application/json")]
public sealed class ManageContentController : ControllerBase
{
    private readonly IContentManagementService _management;

    public ManageContentController(IContentManagementService management)
    {
        _management = management;
    }

    /// <summary>Returns the profile for editing.</summary>
    [RequirePortalPage(PortalPages.ContentProfile)]
    [HttpGet("profile")]
    [ProducesResponseType<ProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ProfileDto>> GetProfileAsync(CancellationToken cancellationToken)
    {
        var profile = await _management.GetProfileAsync(cancellationToken).ConfigureAwait(false);

        return profile.IsSuccess
            ? Ok(profile.Value)
            : ProblemResults.FromResult(HttpContext, profile);
    }

    /// <summary>
    /// Creates or replaces the profile.
    /// <para>
    /// A PUT with no id creates it, because the portal has exactly one profile: there is no collection to
    /// append to and no alternative address to post to.
    /// </para>
    /// </summary>
    [RequirePortalPage(PortalPages.ContentProfile)]
    [HttpPut("profile")]
    [ProducesResponseType<ProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ProfileDto>> SaveProfileAsync(
        [FromBody] ProfileRequest request,
        CancellationToken cancellationToken)
    {
        var saved = await _management.SaveProfileAsync(request, cancellationToken).ConfigureAwait(false);

        return saved.IsSuccess
            ? Ok(saved.Value)
            : ProblemResults.FromResult(HttpContext, saved);
    }

    /// <summary>Lists every project, published or not, in display order.</summary>
    [RequirePortalPage(PortalPages.ContentProjects)]
    [HttpGet("projects")]
    [ProducesResponseType<IReadOnlyList<ManagedProjectDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<ManagedProjectDto>>> ListProjectsAsync(
        CancellationToken cancellationToken)
    {
        var projects = await _management.ListProjectsAsync(cancellationToken).ConfigureAwait(false);

        return projects.IsSuccess
            ? Ok(projects.Value)
            : ProblemResults.FromResult(HttpContext, projects);
    }

    /// <summary>Creates or replaces a project, addressed by <see cref="ProjectRequest.Id"/>.</summary>
    [RequirePortalPage(PortalPages.ContentProjects)]
    [HttpPut("projects")]
    [ProducesResponseType<ProjectDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ManagedProjectDto>> SaveProjectAsync(
        [FromBody] ProjectRequest request,
        CancellationToken cancellationToken)
    {
        var saved = await _management.SaveProjectAsync(request, cancellationToken).ConfigureAwait(false);

        return saved.IsSuccess
            ? Ok(saved.Value)
            : ProblemResults.FromResult(HttpContext, saved);
    }

    /// <summary>Publishes or unpublishes a project without touching its other fields.</summary>
    [RequirePortalPage(PortalPages.ContentProjects)]
    [HttpPost("projects/{projectId:guid}/published")]
    [ProducesResponseType<ProjectDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ManagedProjectDto>> SetPublishedAsync(
        Guid projectId,
        [FromBody] SetPublishedRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _management
            .SetProjectPublishedAsync(projectId, request.IsPublished, cancellationToken)
            .ConfigureAwait(false);

        return updated.IsSuccess
            ? Ok(updated.Value)
            : ProblemResults.FromResult(HttpContext, updated);
    }

    /// <summary>Deletes a project and its technology associations.</summary>
    [RequirePortalPage(PortalPages.ContentProjects)]
    [HttpDelete("projects/{projectId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteProjectAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var deleted = await _management.DeleteProjectAsync(projectId, cancellationToken).ConfigureAwait(false);

        return deleted.IsSuccess
            ? NoContent()
            : ProblemResults.FromResult(HttpContext, deleted);
    }
}

/// <summary>Body for <see cref="ManageContentController.SetPublishedAsync"/>.</summary>
/// <param name="IsPublished">Target published state.</param>
public sealed record SetPublishedRequest(bool IsPublished);
