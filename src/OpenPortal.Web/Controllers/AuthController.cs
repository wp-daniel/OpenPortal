using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Application.Contracts;
using OpenPortal.SharedKernel.Results;
using OpenPortal.Web.Infrastructure;

namespace OpenPortal.Web.Controllers;

/// <summary>
/// Sign-in, sign-out and session inspection.
/// <para>
/// Deliberately anonymous: these are the endpoints an unauthenticated caller reaches.
/// </para>
/// </summary>
[ApiController]
[Route("api/auth")]
[AllowAnonymous]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authentication;
    private readonly IAntiforgery _antiforgery;

    public AuthController(IAuthenticationService authentication, IAntiforgery antiforgery)
    {
        _authentication = authentication;
        _antiforgery = antiforgery;
    }

    /// <summary>
    /// Issues an antiforgery token for this browser session.
    /// <para>
    /// Every unsafe request is validated against one of these, including sign-in, so the client must call
    /// this before its first POST. Calling it is what writes the token cookie; the returned value is what
    /// goes in the <c>X-XSRF-TOKEN</c> header.
    /// </para>
    /// </summary>
    [HttpGet("antiforgery")]
    [ProducesResponseType<AntiforgeryTokenDto>(StatusCodes.Status200OK)]
    public ActionResult<AntiforgeryTokenDto> GetAntiforgeryToken()
    {
        var tokens = _antiforgery.GetAndStoreTokens(HttpContext);

        // GetAndStoreTokens can legitimately return no request token only if the host disabled the default
        // generator, which would leave every unsafe request unverifiable. Fail loudly rather than returning
        // an empty token the client would send forever.
        return string.IsNullOrEmpty(tokens.RequestToken)
            ? throw new InvalidOperationException(
                "Antiforgery is configured without a request token generator, so no unsafe request can be validated.")
            : Ok(new AntiforgeryTokenDto(tokens.RequestToken, AntiforgeryDefaults.HeaderName));
    }

    /// <summary>Establishes a session from email and password.</summary>
    [HttpPost("login")]
    [ProducesResponseType<SessionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SessionDto>> LoginAsync(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var signedIn = await _authentication.SignInAsync(request, cancellationToken).ConfigureAwait(false);
        if (signedIn.IsFailure)
        {
            return ProblemResults.FromResult(HttpContext, signedIn);
        }

        // The response carries the session the cookie now represents, so the client can render the signed-in
        // state without a second round trip. The fallback is unreachable in practice: GetSessionAsync already
        // converts every failure it can anticipate into an anonymous session. It stays because the endpoint
        // promises "always 200" and a client polling it has no way to recover from a 500.
        var session = await _authentication.GetSessionAsync(cancellationToken).ConfigureAwait(false);

        return session.IsSuccess
            ? Ok(session.Value)
            : Ok(SessionDto.AnonymousFor(_authentication.PasswordPolicy));
    }

    /// <summary>Terminates the session and clears its cookie.</summary>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LogoutAsync(CancellationToken cancellationToken)
    {
        await _authentication.SignOutAsync(cancellationToken).ConfigureAwait(false);

        return NoContent();
    }

    /// <summary>
    /// Reports the current session. Always 200, including for anonymous callers, so the client can use it as
    /// the single source of truth on whether anyone is signed in.
    /// </summary>
    [HttpGet("session")]
    [ProducesResponseType<SessionDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SessionDto>> GetSessionAsync(CancellationToken cancellationToken)
    {
        var session = await _authentication.GetSessionAsync(cancellationToken).ConfigureAwait(false);

        return session.IsSuccess
            ? Ok(session.Value)
            : Ok(SessionDto.AnonymousFor(_authentication.PasswordPolicy));
    }
}