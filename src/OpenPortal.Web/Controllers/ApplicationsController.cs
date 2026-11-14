using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OpenPortal.Web.Security;
using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Access.Application.Contracts;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.SharedKernel.Text;
using OpenPortal.Web.Infrastructure;

using OpenPortal.Web.Authorization;

namespace OpenPortal.Web.Controllers;

/// <summary>The signed-in user's launchpad: the applications they can open.</summary>
[ApiController]
[Route("api/account/applications")]
[Authorize]
[Produces("application/json")]
public sealed class LaunchpadController : ControllerBase
{
    private readonly IAccessEvaluator _access;
    private readonly ICurrentUser _currentUser;
    private readonly string? _iconDirectory;

    public LaunchpadController(IAccessEvaluator access, ICurrentUser currentUser, IConfiguration configuration)
    {
        _access = access;
        _currentUser = currentUser;
        _iconDirectory = configuration["Launchpad:IconDirectory"];
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<LaunchpadItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<LaunchpadItemDto>>> ListAsync(CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not Guid userId)
        {
            return Unauthorized();
        }

        var apps = await _access.GetLaunchpadAsync(userId, cancellationToken).ConfigureAwait(false);
        return Ok(apps.Select(app => FindIcon(app.ClientId) is { } icon
            ? app with { IconUrl = Url.Action(nameof(IconAsync), new { clientId = app.ClientId, v = icon.LastWriteTimeUtc.Ticks }) }
            : app).ToArray());
    }

    [HttpGet("{clientId}/icon")]
    [Produces("image/png")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> IconAsync(string clientId, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not Guid userId)
        {
            return Unauthorized();
        }

        var icon = FindIcon(clientId);
        if (icon is null || !(await _access.EvaluateAsync(userId, clientId, cancellationToken).ConfigureAwait(false)).Allowed)
        {
            return NotFound();
        }

        return PhysicalFile(icon.FullName, "image/png");
    }

    private FileInfo? FindIcon(string clientId)
    {
        if (string.IsNullOrWhiteSpace(_iconDirectory) || !TextRules.TryNormaliseSlug(clientId, out var slug) || slug != clientId)
        {
            return null;
        }

        var file = new FileInfo(Path.Combine(_iconDirectory, slug + ".png"));
        return file.Exists ? file : null;
    }
}

/// <summary>
/// Where running applications announce themselves. Authenticated by the provisioning key rather than a
/// session, and called server-to-server, so neither the session cookie nor antiforgery applies.
/// </summary>
[ApiController]
[Route("api/apps")]
[AllowAnonymous]
[IgnoreAntiforgeryToken]
[AllowDuringTwoFactorSetup]
[Produces("application/json")]
public sealed class ApplicationAnnouncementsController : ControllerBase
{
    /// <summary>Header carrying the provisioning key.</summary>
    public const string ProvisioningKeyHeader = "X-OpenPortal-Provisioning-Key";

    private readonly IApplicationAnnouncementService _announcements;

    public ApplicationAnnouncementsController(IApplicationAnnouncementService announcements)
    {
        _announcements = announcements;
    }

    [HttpPost("announce")]
    [EnableRateLimiting(RateLimitPolicies.Machine)]
    [ProducesResponseType<AnnouncementResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AnnouncementResultDto>> AnnounceAsync(
        [FromBody] ApplicationManifest manifest,
        [FromHeader(Name = ProvisioningKeyHeader)] string? provisioningKey,
        CancellationToken cancellationToken)
    {
        var result = await _announcements.AnnounceAsync(manifest, provisioningKey, cancellationToken).ConfigureAwait(false);

        return result.IsSuccess ? Ok(result.Value) : ProblemResults.FromResult(HttpContext, result);
    }
}
