using System.Security.Claims;
using OpenIddict.Abstractions;
using OpenPortal.Access.Application.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace OpenPortal.Web.Authentication;

/// <summary>
/// The claim that marks an access token an application obtained for itself (client credentials), so it can never
/// be mistaken for a user's token: the user flows never set it.
/// </summary>
public static class ApplicationTokens
{
    public const string CallerClaim = "portal_caller";

    public const string ApplicationCaller = "application";

    /// <summary>True for the principal of an application's own token; its subject is then the client id.</summary>
    public static bool IsApplication(ClaimsPrincipal? principal) =>
        principal?.Identity?.IsAuthenticated == true
        && string.Equals(principal.GetClaim(CallerClaim), ApplicationCaller, StringComparison.Ordinal)
        && !string.IsNullOrEmpty(principal.GetClaim(Claims.Subject));
}

/// <summary>
/// Reads the calling application from the request's bearer token. Only the principal of an endpoint that
/// authenticates with OpenIddict's validation scheme can carry the marker, so a cookie session never counts as
/// an application.
/// </summary>
internal sealed class HttpApplicationApiCaller : IApplicationApiCaller
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpApplicationApiCaller(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public string? ClientId =>
        ApplicationTokens.IsApplication(Principal) ? Principal!.GetClaim(Claims.Subject) : null;

    public bool HasScope(string scope) =>
        ApplicationTokens.IsApplication(Principal) && Principal!.HasScope(scope);
}
