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
/// Registered as scoped and resolved per request. Outside a request (for example a startup task) the
/// <see cref="IHttpContextAccessor"/> returns no context, and this reports an anonymous caller rather than
/// throwing: a background task genuinely has no caller.
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

    public Guid? UserId
    {
        get
        {
            var value = Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(value, out var userId) ? userId : null;
        }
    }

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
}