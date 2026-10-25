using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using OpenPortal.Identity.Application.Abstractions;

namespace OpenPortal.Web.Authentication;

/// <summary>
/// Reads the caller from <see cref="HttpContext.User"/>.
/// <para>
/// This is the only place where "who is calling" is derived from the transport. Everything below the
/// controller receives <see cref="ICurrentUser"/>, so the application services stay testable and free of
/// any HTTP dependency.
/// </para>
/// <para>
/// Registered as a singleton that reads the request through <see cref="IHttpContextAccessor"/>. Outside a
/// request (for example a startup task) the accessor returns no context, and this reports an anonymous caller
/// rather than throwing: a background task genuinely has no caller.
/// </para>
/// </summary>
internal sealed class HttpContextCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public Guid? UserId => GuidClaim(ClaimTypes.NameIdentifier);

    public string? Email => Principal?.FindFirstValue(ClaimTypes.Email)
        ?? Principal?.FindFirstValue(ClaimTypes.Name);

    public IReadOnlyCollection<string> Roles
    {
        get
        {
            var principal = Principal;
            if (principal is null)
            {
                return [];
            }

            var roles = principal.FindAll(ClaimTypes.Role)
                .Select(claim => claim.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            return roles;
        }
    }

    public Guid? ImpersonatorId => GuidClaim(PortalClaimTypes.ImpersonatorId);

    public string? ImpersonatorEmail => ImpersonatorId is null ? null : Principal?.FindFirstValue(PortalClaimTypes.ImpersonatorEmail);

    public DateTimeOffset? ImpersonationStartedAtUtc =>
        ImpersonatorId is not null
        && long.TryParse(Principal?.FindFirstValue(PortalClaimTypes.ImpersonationStarted), NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : null;

    public Guid? PersonalAccessTokenId => GuidClaim(PortalClaimTypes.PersonalAccessTokenId);

    public IReadOnlyList<string> AuthenticationMethods => SessionAuthentication.Methods(Principal);

    public string? SessionId =>
        PersonalAccessTokenId is null ? _httpContextAccessor.HttpContext?.Items[SessionItems.CurrentSessionId] as string : null;

    private Guid? GuidClaim(string type) =>
        Guid.TryParse(Principal?.FindFirstValue(type), out var value) ? value : null;
}
