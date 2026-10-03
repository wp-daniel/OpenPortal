using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenPortal.Content.Application.Abstractions;
using OpenPortal.Content.Application.Contracts;
using OpenPortal.Content.Domain;
using OpenPortal.Content.Domain.Projects;
using OpenPortal.SharedKernel.Results;
using OpenPortal.Web.Infrastructure;

namespace OpenPortal.Web.Controllers;

/// <summary>Read-only public content, served without authentication.</summary>
[ApiController]
[Route("api/content")]
[AllowAnonymous]
[Produces("application/json")]
public sealed class ContentController : ControllerBase
{
    private readonly IPublicContentService _publicContent;

    public ContentController(IPublicContentService publicContent)
    {
        _publicContent = publicContent;
    }

    /// <summary>
    /// Everything the public site renders, in one payload: the profile and its published projects. The client
    /// needs a single round trip for the landing page rather than one per section.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<PublicContentDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PublicContentDto>> GetAsync(CancellationToken cancellationToken)
    {
        var content = await _publicContent.GetPublicContentAsync(cancellationToken).ConfigureAwait(false);

        return content.IsSuccess
            ? Ok(content.Value)
            : ProblemResults.FromResult(HttpContext, content);
    }

    /// <summary>One published project, addressed by its stable slug.</summary>
    [HttpGet("projects/{slug}")]
    [ProducesResponseType<ProjectDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectDto>> GetProjectAsync(string slug, CancellationToken cancellationToken)
    {
        // An out-of-range or unparseable slug is reported as "not found" rather than as a validation error,
        // so this endpoint does not double as a slug validator and cannot be used to probe for valid values.
        if (string.IsNullOrWhiteSpace(slug) || slug.Length > Project.SlugMaxLength)
        {
            return ProblemResults.FromResult(
                HttpContext,
                Result<ProjectDto>.Failure(ContentErrors.ProjectNotFound));
        }

        var project = await _publicContent.GetProjectAsync(slug, cancellationToken).ConfigureAwait(false);

        return project.IsSuccess
            ? Ok(project.Value)
            : ProblemResults.FromResult(HttpContext, project);
    }
}