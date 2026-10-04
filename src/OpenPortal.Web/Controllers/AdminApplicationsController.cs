using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Access.Application.Contracts;
using OpenPortal.SharedKernel.Results;
using OpenPortal.Web.Authorization;
using OpenPortal.Web.Infrastructure;

namespace OpenPortal.Web.Controllers;

/// <summary>Administrator-only registry of the applications that sign in through the portal.</summary>
[ApiController]
[Route("api/admin/applications")]
[Authorize(Policy = Policies.AdministratorOnly)]
[Produces("application/json")]
public sealed class AdminApplicationsController : ControllerBase
{
    private readonly IApplicationRegistryService _applications;

    public AdminApplicationsController(IApplicationRegistryService applications)
    {
        _applications = applications;
    }

    /// <summary>Lists every application, waiting ones first.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ApplicationDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<ApplicationDto>>> ListAsync(CancellationToken cancellationToken) =>
        Respond(await _applications.ListAsync(cancellationToken).ConfigureAwait(false));

    [HttpGet("{applicationId:guid}")]
    [ProducesResponseType<ApplicationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApplicationDto>> GetAsync(Guid applicationId, CancellationToken cancellationToken) =>
        Respond(await _applications.GetAsync(applicationId, cancellationToken).ConfigureAwait(false));

    /// <summary>Registers an application by hand. The response carries its client secret, shown once.</summary>
    [HttpPost]
    [ProducesResponseType<ApplicationSecretDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApplicationSecretDto>> CreateAsync(
        [FromBody] CreateApplicationRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _applications.CreateAsync(request, cancellationToken).ConfigureAwait(false);
        if (created.IsFailure)
        {
            return ProblemResults.FromResult(HttpContext, created);
        }

        return CreatedAtAction(nameof(GetAsync), new { applicationId = created.Value.Application.Id }, created.Value);
    }

    [HttpPut("{applicationId:guid}")]
    [ProducesResponseType<ApplicationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApplicationDto>> UpdateAsync(
        Guid applicationId,
        [FromBody] UpdateApplicationRequest request,
        CancellationToken cancellationToken) =>
        Respond(await _applications.UpdateAsync(applicationId, request, cancellationToken).ConfigureAwait(false));

    /// <summary>Approves an announced application. The response carries its client secret, shown once.</summary>
    [HttpPost("{applicationId:guid}/approve")]
    [ProducesResponseType<ApplicationSecretDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApplicationSecretDto>> ApproveAsync(Guid applicationId, CancellationToken cancellationToken) =>
        Respond(await _applications.ApproveAsync(applicationId, cancellationToken).ConfigureAwait(false));

    /// <summary>Issues a new client secret; the previous one stops working.</summary>
    [HttpPost("{applicationId:guid}/secret")]
    [ProducesResponseType<ApplicationSecretDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApplicationSecretDto>> RegenerateSecretAsync(Guid applicationId, CancellationToken cancellationToken) =>
        Respond(await _applications.RegenerateSecretAsync(applicationId, cancellationToken).ConfigureAwait(false));

    /// <summary>Adopts the redirect URIs the application last announced.</summary>
    [HttpPost("{applicationId:guid}/apply-manifest")]
    [ProducesResponseType<ApplicationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApplicationDto>> ApplyManifestAsync(Guid applicationId, CancellationToken cancellationToken) =>
        Respond(await _applications.ApplyManifestAsync(applicationId, cancellationToken).ConfigureAwait(false));

    [HttpPost("{applicationId:guid}/disable")]
    [ProducesResponseType<ApplicationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApplicationDto>> DisableAsync(Guid applicationId, CancellationToken cancellationToken) =>
        Respond(await _applications.DisableAsync(applicationId, cancellationToken).ConfigureAwait(false));

    [HttpPost("{applicationId:guid}/enable")]
    [ProducesResponseType<ApplicationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApplicationDto>> EnableAsync(Guid applicationId, CancellationToken cancellationToken) =>
        Respond(await _applications.EnableAsync(applicationId, cancellationToken).ConfigureAwait(false));

    [HttpDelete("{applicationId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        var deleted = await _applications.DeleteAsync(applicationId, cancellationToken).ConfigureAwait(false);

        return deleted.IsSuccess ? NoContent() : ProblemResults.FromResult(HttpContext, deleted);
    }

    private ActionResult<T> Respond<T>(Result<T> result) =>
        result.IsSuccess ? Ok(result.Value) : ProblemResults.FromResult(HttpContext, result);
}
