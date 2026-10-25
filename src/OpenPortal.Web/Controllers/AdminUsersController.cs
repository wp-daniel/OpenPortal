using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Application.Contracts;
using OpenPortal.Identity.Domain.Users;
using OpenPortal.SharedKernel.Results;
using OpenPortal.Web.Authorization;
using OpenPortal.Web.Infrastructure;
using OpenPortal.Web.Users;

namespace OpenPortal.Web.Controllers;

/// <summary>Account management, for administrators and holders of the users page.</summary>
/// <remarks>
/// <para>
/// The page check here is the first gate. Every service behind these routes re-checks the caller before
/// mutating anything, so the endpoints cannot be bypassed by calling the service from elsewhere.
/// </para>
/// </remarks>
[ApiController]
[Route("api/admin/users")]
[Authorize]
[Produces("application/json")]
public sealed class AdminUsersController : ControllerBase
{
    private readonly IUserAdministrationService _users;
    private readonly AccountEraser _eraser;
    private readonly IImpersonationService _impersonation;
    private readonly UserCsv _csv;
    private readonly UserBulkActions _bulk;
    private readonly IPersonalAccessTokenService _tokens;
    private readonly ICurrentUser _currentUser;

    public AdminUsersController(
        IUserAdministrationService users,
        AccountEraser eraser,
        IImpersonationService impersonation,
        UserCsv csv,
        UserBulkActions bulk,
        IPersonalAccessTokenService tokens,
        ICurrentUser currentUser)
    {
        _users = users;
        _eraser = eraser;
        _impersonation = impersonation;
        _csv = csv;
        _bulk = bulk;
        _tokens = tokens;
        _currentUser = currentUser;
    }

    /// <summary>Lists accounts, filtered and paged.</summary>
    [RequirePortalPage(PortalPages.Users, PortalPages.Groups, PortalPages.Access)]
    [HttpGet]
    [ProducesResponseType<PagedResult<UserSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<UserSummaryDto>>> ListAsync(
        [FromQuery] UserListQuery query,
        CancellationToken cancellationToken)
    {
        var users = await _users.ListUsersAsync(query, cancellationToken).ConfigureAwait(false);

        return users.IsSuccess
            ? Ok(users.Value)
            : ProblemResults.FromResult(HttpContext, users);
    }

    /// <summary>Returns one account.</summary>
    [RequirePortalPage(PortalPages.Users)]
    [HttpGet("{userId:guid}")]
    [ProducesResponseType<UserSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UserSummaryDto>> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _users.GetUserAsync(userId, cancellationToken).ConfigureAwait(false);

        return user.IsSuccess
            ? Ok(user.Value)
            : ProblemResults.FromResult(HttpContext, user);
    }

    /// <summary>Creates an account.</summary>
    [RequirePortalPage(PortalPages.Users)]
    [HttpPost]
    [ProducesResponseType<UserSummaryDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UserSummaryDto>> CreateAsync(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _users.CreateUserAsync(request, cancellationToken).ConfigureAwait(false);
        if (created.IsFailure)
        {
            return ProblemResults.FromResult(HttpContext, created);
        }

        return CreatedAtAction(nameof(GetAsync), new { userId = created.Value.Id }, created.Value);
    }

    /// <summary>Updates an account's display name and role membership.</summary>
    [RequirePortalPage(PortalPages.Users)]
    [HttpPut("{userId:guid}")]
    [ProducesResponseType<UserSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UserSummaryDto>> UpdateAsync(
        Guid userId,
        [FromBody] UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _users.UpdateUserAsync(userId, request, cancellationToken).ConfigureAwait(false);

        return updated.IsSuccess
            ? Ok(updated.Value)
            : ProblemResults.FromResult(HttpContext, updated);
    }

    /// <summary>Sets a new password and ends that account's existing sessions.</summary>
    [RequirePortalPage(PortalPages.Users)]
    [HttpPost("{userId:guid}/reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ResetPasswordAsync(
        Guid userId,
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var reset = await _users.ResetPasswordAsync(userId, request, cancellationToken).ConfigureAwait(false);

        return reset.IsSuccess
            ? NoContent()
            : ProblemResults.FromResult(HttpContext, reset);
    }

    /// <summary>An account's open sessions and its recent sign-ins.</summary>
    [RequirePortalPage(PortalPages.Users)]
    [HttpGet("{userId:guid}/sessions")]
    [ProducesResponseType<AccountSessionsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountSessionsDto>> ListSessionsAsync(
        Guid userId,
        [FromServices] ISessionService sessions,
        CancellationToken cancellationToken)
    {
        var list = await sessions.ListForUserAsync(userId, cancellationToken).ConfigureAwait(false);

        return list.IsSuccess ? Ok(list.Value) : ProblemResults.FromResult(HttpContext, list);
    }

    /// <summary>Ends one session of an account (a browser left signed in on a shared computer).</summary>
    [RequirePortalPage(PortalPages.Users)]
    [HttpDelete("{userId:guid}/sessions/{sessionId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RevokeSessionAsync(
        Guid userId,
        string sessionId,
        [FromServices] ISessionService sessions,
        CancellationToken cancellationToken)
    {
        var revoked = await sessions.RevokeForUserAsync(userId, sessionId, cancellationToken).ConfigureAwait(false);

        return revoked.IsSuccess ? NoContent() : ProblemResults.FromResult(HttpContext, revoked);
    }

    /// <summary>The external accounts (Google, Microsoft) connected to an account.</summary>
    [RequirePortalPage(PortalPages.Users)]
    [HttpGet("{userId:guid}/external-logins")]
    [ProducesResponseType<IReadOnlyList<ExternalLoginDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ExternalLoginDto>>> ListExternalLoginsAsync(
        Guid userId,
        [FromServices] IExternalLoginService externalLogins,
        CancellationToken cancellationToken)
    {
        var logins = await externalLogins.ListForUserAsync(userId, cancellationToken).ConfigureAwait(false);

        return logins.IsSuccess ? Ok(logins.Value) : ProblemResults.FromResult(HttpContext, logins);
    }

    /// <summary>Disconnects an external account from an account (it was compromised), ending the sessions it opened.</summary>
    [RequirePortalPage(PortalPages.Users)]
    [HttpDelete("{userId:guid}/external-logins/{provider}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RemoveExternalLoginAsync(
        Guid userId,
        string provider,
        [FromServices] IExternalLoginService externalLogins,
        CancellationToken cancellationToken)
    {
        var removed = await externalLogins.RemoveForUserAsync(userId, provider, cancellationToken).ConfigureAwait(false);

        return removed.IsSuccess ? NoContent() : ProblemResults.FromResult(HttpContext, removed);
    }

    /// <summary>Removes every passkey of an account that lost its devices, ending its sessions.</summary>
    [RequirePortalPage(PortalPages.Users)]
    [HttpDelete("{userId:guid}/passkeys")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ResetPasskeysAsync(
        Guid userId,
        [FromServices] IPasskeyService passkeys,
        CancellationToken cancellationToken)
    {
        var reset = await passkeys.ResetForUserAsync(userId, cancellationToken).ConfigureAwait(false);

        return reset.IsSuccess
            ? NoContent()
            : ProblemResults.FromResult(HttpContext, reset);
    }

    /// <summary>Turns two-factor authentication off for an account that lost its authenticator, ending its sessions.</summary>
    [RequirePortalPage(PortalPages.Users)]
    [HttpDelete("{userId:guid}/two-factor")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ResetTwoFactorAsync(Guid userId, CancellationToken cancellationToken)
    {
        var reset = await _users.ResetTwoFactorAsync(userId, cancellationToken).ConfigureAwait(false);

        return reset.IsSuccess
            ? NoContent()
            : ProblemResults.FromResult(HttpContext, reset);
    }

    /// <summary>
    /// Lets a self-registered account that waits for approval sign in, and tells its owner. To reject one, delete it.
    /// </summary>
    [RequirePortalPage(PortalPages.Users)]
    [HttpPost("{userId:guid}/approve")]
    [ProducesResponseType<UserSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserSummaryDto>> ApproveAsync(Guid userId, CancellationToken cancellationToken)
    {
        var approved = await _users.ApproveRegistrationAsync(userId, cancellationToken).ConfigureAwait(false);

        return approved.IsSuccess ? Ok(approved.Value) : ProblemResults.FromResult(HttpContext, approved);
    }

    /// <summary>
    /// Deletes an account, then what the Access module holds about it (group memberships, direct grants and
    /// tokens). Nobody can delete themselves; only an administrator can delete an administrator.
    /// </summary>
    [RequirePortalPage(PortalPages.Users)]
    [HttpDelete("{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteAsync(Guid userId, CancellationToken cancellationToken)
    {
        var deleted = await _eraser.DeleteAsync(userId, cancellationToken).ConfigureAwait(false);

        return deleted.IsSuccess ? NoContent() : ProblemResults.FromResult(HttpContext, deleted);
    }

    /// <summary>
    /// Starts a support session as an ordinary account: the caller's session becomes one as that account, marked
    /// as a support session, until they end it or <c>Identity:ImpersonationMinutes</c> pass. Audited, and the
    /// account's owner is told. Administrators only, from their own browser session.
    /// </summary>
    [Authorize(Policy = Policies.AdministratorOnly)]
    [InteractiveSessionOnly]
    [HttpPost("{userId:guid}/impersonate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ImpersonateAsync(
        Guid userId,
        [FromBody] StartImpersonationRequest request,
        CancellationToken cancellationToken)
    {
        var started = await _impersonation.StartAsync(userId, request, cancellationToken).ConfigureAwait(false);

        return started.IsSuccess ? NoContent() : ProblemResults.FromResult(HttpContext, started);
    }

    /// <summary>
    /// Downloads the accounts matching the filter (or the selected <c>ids</c>) as CSV, sorted by email, at most
    /// 10,000 rows. The administrator column is there only for administrators. Recorded in the audit log.
    /// </summary>
    [RequirePortalPage(PortalPages.Users)]
    [HttpGet("export")]
    [Produces("text/csv", "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExportAsync([FromQuery] UserListFilter filter, CancellationToken cancellationToken)
    {
        var users = await _users.ExportUsersAsync(filter, UserBulkLimits.MaxExportRows, cancellationToken).ConfigureAwait(false);
        if (users.IsFailure)
        {
            return ProblemResults.FromResult(HttpContext, users);
        }

        var isAdministrator = _currentUser.Roles.Contains(Roles.Administrator, StringComparer.Ordinal);
        var fileName = $"openportal-users-{DateTime.UtcNow.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture)}.csv";

        return File(UserCsv.Export(users.Value, isAdministrator), Csv.ContentType, fileName);
    }

    /// <summary>
    /// Imports a CSV (a <c>file</c> part, at most 1 MB and 1,000 rows, with an <c>email</c> column): new addresses
    /// are invited (with <c>firstName</c>, <c>lastName</c>, <c>groups</c> and <c>organizations</c>, names separated by
    /// <c>|</c>), existing accounts are updated when <c>updateExisting</c> is true and skipped otherwise. With
    /// <c>dryRun</c> nothing is written: the answer says what would happen to each row.
    /// </summary>
    [RequirePortalPage(PortalPages.Users)]
    [HttpPost("import")]
    [RequestSizeLimit(UserCsv.MaxImportBytes + 64 * 1024)]
    [ProducesResponseType<UserImportResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UserImportResultDto>> ImportAsync(
        IFormFile? file,
        [FromQuery] bool dryRun,
        [FromQuery] bool updateExisting,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0 || file.Length > UserCsv.MaxImportBytes)
        {
            return ProblemResults.FromResult(HttpContext, Result.Failure(UserImportErrors.FileInvalid));
        }

        string text;
        using (var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
        {
            text = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }

        var imported = await _csv.ImportAsync(text, dryRun, updateExisting, cancellationToken).ConfigureAwait(false);

        return imported.IsSuccess ? Ok(imported.Value) : ProblemResults.FromResult(HttpContext, imported);
    }

    /// <summary>
    /// One action on up to 100 accounts: <c>approve</c>, <c>add_to_group</c> / <c>remove_from_group</c> (with
    /// <c>groupId</c>), <c>sign_out</c> (every session and token) or <c>delete</c>. Each account is checked as if
    /// handled alone; the answer says what happened to each.
    /// </summary>
    [RequirePortalPage(PortalPages.Users)]
    [HttpPost("bulk")]
    [ProducesResponseType<IReadOnlyList<UserBulkResultDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<UserBulkResultDto>>> BulkAsync(
        [FromBody] UserBulkRequest request,
        CancellationToken cancellationToken)
    {
        var results = await _bulk.RunAsync(request, cancellationToken).ConfigureAwait(false);

        return results.IsSuccess ? Ok(results.Value) : ProblemResults.FromResult(HttpContext, results);
    }

    /// <summary>The account's personal access tokens (never their value).</summary>
    [RequirePortalPage(PortalPages.Users)]
    [HttpGet("{userId:guid}/tokens")]
    [ProducesResponseType<IReadOnlyList<PersonalAccessTokenDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<PersonalAccessTokenDto>>> ListTokensAsync(Guid userId, CancellationToken cancellationToken)
    {
        var tokens = await _tokens.ListForUserAsync(userId, cancellationToken).ConfigureAwait(false);

        return tokens.IsSuccess ? Ok(tokens.Value) : ProblemResults.FromResult(HttpContext, tokens);
    }

    /// <summary>Revokes one of the account's personal access tokens.</summary>
    [RequirePortalPage(PortalPages.Users)]
    [HttpDelete("{userId:guid}/tokens/{tokenId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeTokenAsync(Guid userId, Guid tokenId, CancellationToken cancellationToken)
    {
        var revoked = await _tokens.RevokeForUserAsync(userId, tokenId, cancellationToken).ConfigureAwait(false);

        return revoked.IsSuccess ? NoContent() : ProblemResults.FromResult(HttpContext, revoked);
    }
}
