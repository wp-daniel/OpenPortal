using OpenPortal.Audit.Application.Abstractions;
using OpenPortal.Audit.Domain;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.SharedKernel.Auditing;

namespace OpenPortal.Web.Auditing;

/// <summary>
/// Tells the Audit module who is calling and from where, read from the current request.
/// <para>
/// The address is <see cref="ConnectionInfo.RemoteIpAddress"/>, which is the client's own address only when
/// the forwarded headers of a trusted reverse proxy have been applied (see <c>ReverseProxy</c> in the
/// settings); otherwise it is the proxy's. The correlation id is the request's trace identifier, the same
/// value an error response reports as <c>traceId</c>, so a failure a user reports can be found in the log.
/// </para>
/// </summary>
internal sealed class HttpAuditRequestContext : IAuditRequestContext
{
    private readonly IHttpContextAccessor _http;
    private readonly ICurrentUser _currentUser;

    public HttpAuditRequestContext(IHttpContextAccessor http, ICurrentUser currentUser)
    {
        _http = http;
        _currentUser = currentUser;
    }

    public AuditSubject? Actor =>
        _currentUser.UserId is { } userId
            ? new AuditSubject(AuditSubjectTypes.User, userId.ToString(), _currentUser.Email)
            : null;

    public AuditOrigin Origin
    {
        get
        {
            var context = _http.HttpContext;
            if (context is null)
            {
                return AuditOrigin.None;
            }

            var userAgent = context.Request.Headers.UserAgent.ToString();

            return new AuditOrigin(
                context.Connection.RemoteIpAddress?.ToString(),
                string.IsNullOrWhiteSpace(userAgent) ? null : userAgent,
                context.TraceIdentifier);
        }
    }
}
