using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Application.Contracts;
using OpenPortal.Web.Authorization;
using OpenPortal.Web.Infrastructure;

namespace OpenPortal.Web.Controllers;

/// <summary>
/// The signed-in account's passkeys. Adding, renaming and removing them is for the account holder's own browser
/// session only; the options and credentials are the JSON the browser's WebAuthn API reads and writes.
/// </summary>
[ApiController]
[Route("api/account/passkeys")]
[Authorize]
[AllowDuringTwoFactorSetup]
[Produces("application/json")]
public sealed class AccountPasskeysController : ControllerBase
{
    private readonly IPasskeyService _passkeys;

    public AccountPasskeysController(IPasskeyService passkeys)
    {
        _passkeys = passkeys;
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PasskeyDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PasskeyDto>>> ListAsync(CancellationToken cancellationToken)
    {
        var passkeys = await _passkeys.ListAsync(cancellationToken).ConfigureAwait(false);

        return passkeys.IsSuccess ? Ok(passkeys.Value) : ProblemResults.FromResult(HttpContext, passkeys);
    }

    /// <summary>The options for <c>navigator.credentials.create()</c> (a JSON object, passed on as is).</summary>
    [InteractiveSessionOnly]
    [HttpPost("creation-options")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreationOptionsAsync(CancellationToken cancellationToken)
    {
        var options = await _passkeys.CreationOptionsAsync(cancellationToken).ConfigureAwait(false);

        return options.IsSuccess ? Content(options.Value, "application/json") : ProblemResults.FromResult(HttpContext, options);
    }

    [InteractiveSessionOnly]
    [HttpPost]
    [ProducesResponseType<PasskeyDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PasskeyDto>> RegisterAsync([FromBody] RegisterPasskeyRequest request, CancellationToken cancellationToken)
    {
        var passkey = await _passkeys.RegisterAsync(request, cancellationToken).ConfigureAwait(false);

        return passkey.IsSuccess ? Ok(passkey.Value) : ProblemResults.FromResult(HttpContext, passkey);
    }

    [InteractiveSessionOnly]
    [HttpPut("{passkeyId}")]
    [ProducesResponseType<PasskeyDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PasskeyDto>> RenameAsync(
        string passkeyId,
        [FromBody] RenamePasskeyRequest request,
        CancellationToken cancellationToken)
    {
        var passkey = await _passkeys.RenameAsync(passkeyId, request, cancellationToken).ConfigureAwait(false);

        return passkey.IsSuccess ? Ok(passkey.Value) : ProblemResults.FromResult(HttpContext, passkey);
    }

    [InteractiveSessionOnly]
    [HttpDelete("{passkeyId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteAsync(string passkeyId, CancellationToken cancellationToken)
    {
        var deleted = await _passkeys.DeleteAsync(passkeyId, cancellationToken).ConfigureAwait(false);

        return deleted.IsSuccess ? NoContent() : ProblemResults.FromResult(HttpContext, deleted);
    }
}
