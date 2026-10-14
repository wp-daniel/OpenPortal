using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Access.Application.Contracts;
using OpenPortal.Audit.Application.Abstractions;
using OpenPortal.Audit.Application.Contracts;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Application.Auditing;
using OpenPortal.Identity.Application.Contracts;
using OpenPortal.Web.Authorization;
using OpenPortal.Web.Infrastructure;
using OpenPortal.Web.Oidc;

namespace OpenPortal.Web.Controllers;

/// <summary>
/// The administrators' overview: sign-in activity from the audit log, and what the Identity and Access modules hold.
/// Each module re-checks the caller; the host only puts the three answers side by side.
/// </summary>
/// <param name="Activity">Sign-ins per day, failures and the most used applications.</param>
/// <param name="Users">Accounts counted.</param>
/// <param name="Access">Applications, groups, organizations and requests counted.</param>
public sealed record PortalStatisticsDto(ActivityStatisticsDto Activity, UserStatisticsDto Users, AccessStatisticsDto Access);

/// <summary>Filter for the statistics.</summary>
public sealed class StatisticsQuery
{
    /// <summary>Length of the period in days, ending today.</summary>
    [System.ComponentModel.DataAnnotations.Range(1, ActivityStatisticsRequest.MaxDays, ErrorMessage = "validation.days.range")]
    public int Days { get; init; } = 30;

    /// <summary>The viewer's time zone (IANA, e.g. <c>Europe/Rome</c>), so a day is their day; UTC otherwise.</summary>
    [System.ComponentModel.DataAnnotations.StringLength(64, ErrorMessage = "validation.search.max")]
    public string? TimeZone { get; init; }
}

[ApiController]
[Route("api/admin/statistics")]
[Authorize]
[Produces("application/json")]
public sealed class AdminStatisticsController : ControllerBase
{
    private readonly IAuditStatisticsService _activity;
    private readonly IUserStatisticsService _users;
    private readonly IAccessStatisticsService _access;

    public AdminStatisticsController(
        IAuditStatisticsService activity,
        IUserStatisticsService users,
        IAccessStatisticsService access)
    {
        _activity = activity;
        _users = users;
        _access = access;
    }

    [RequirePortalPage(PortalPages.Statistics)]
    [HttpGet]
    [ProducesResponseType<PortalStatisticsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PortalStatisticsDto>> GetAsync([FromQuery] StatisticsQuery query, CancellationToken cancellationToken)
    {
        // The Audit module counts what it is told is a sign-in; the codes belong to Identity and to the host.
        var activity = await _activity.GetActivityAsync(
                new ActivityStatisticsRequest(query.Days, query.TimeZone, IdentityAuditActions.SignIn, OidcAuditActions.SignIn),
                cancellationToken)
            .ConfigureAwait(false);

        if (activity.IsFailure)
        {
            return ProblemResults.FromResult(HttpContext, activity);
        }

        // "New" and "signed in" accounts over the same period the activity covers (from its first day).
        var periodStart = new DateTimeOffset(activity.Value.From.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        var users = await _users.GetAsync(periodStart, cancellationToken).ConfigureAwait(false);
        if (users.IsFailure)
        {
            return ProblemResults.FromResult(HttpContext, users);
        }

        var access = await _access.GetAsync(cancellationToken).ConfigureAwait(false);
        if (access.IsFailure)
        {
            return ProblemResults.FromResult(HttpContext, access);
        }

        return Ok(new PortalStatisticsDto(activity.Value, users.Value, access.Value));
    }
}
