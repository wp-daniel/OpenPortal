using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenPortal.SharedKernel.Auditing;
using OpenPortal.SharedKernel.Jobs;
using OpenPortal.SharedKernel.Results;
using OpenPortal.Web.Authorization;
using OpenPortal.Web.Infrastructure;
using OpenPortal.Web.Operations;

namespace OpenPortal.Web.Controllers;

/// <summary>
/// The state of this instance (version, checks, background jobs, delivery queue, signing keys) and running a job now.
/// Administrators only: it describes the deployment.
/// </summary>
[ApiController]
[Route("api/admin/system")]
[Authorize(Policy = Policies.AdministratorOnly)]
[Produces("application/json")]
public sealed class AdminSystemController : ControllerBase
{
    public static readonly Error JobNotFound = Error.NotFound("system.job_not_found", "There is no such background job.");

    private readonly SystemStatus _status;
    private readonly IJobRegistry _jobs;
    private readonly IAuditTrail _audit;

    public AdminSystemController(SystemStatus status, IJobRegistry jobs, IAuditTrail audit)
    {
        _status = status;
        _jobs = jobs;
        _audit = audit;
    }

    [HttpGet]
    [ProducesResponseType<SystemStatusDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SystemStatusDto>> GetAsync(CancellationToken cancellationToken) =>
        Ok(await _status.GetAsync(cancellationToken).ConfigureAwait(false));

    /// <summary>Runs a background job now on this instance (a sweep that would otherwise wait for its next turn).</summary>
    [HttpPost("jobs/{name}/run")]
    [ProducesResponseType<JobRunDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JobRunDto>> RunAsync(string name, CancellationToken cancellationToken)
    {
        if (_jobs.Snapshot().All(job => job.Name != name))
        {
            return ProblemResults.FromResult(HttpContext, Result.Failure(JobNotFound));
        }

        int? result = null;
        string? error = null;
        try
        {
            result = await _jobs.RunAsync(name, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Recorded by the registry; the page shows it next to the job.
            error = exception.Message;
        }

        await _audit.RecordAsync(
                new AuditEvent
                {
                    Action = SystemAuditActions.JobRun,
                    Outcome = error is null ? AuditOutcome.Success : AuditOutcome.Failure,
                    Details = new Dictionary<string, string?>
                    {
                        ["job"] = name,
                        ["result"] = result?.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    },
                },
                cancellationToken)
            .ConfigureAwait(false);

        return Ok(new JobRunDto(name, error is null, result, error));
    }
}

/// <summary>How a job run on demand went.</summary>
public sealed record JobRunDto(string Name, bool Succeeded, int? Result, string? Error);

/// <summary>The audit actions of the system page.</summary>
public static class SystemAuditActions
{
    /// <summary>An administrator ran a background job now; details carry its name and result.</summary>
    public const string JobRun = "system.job_run";
}
