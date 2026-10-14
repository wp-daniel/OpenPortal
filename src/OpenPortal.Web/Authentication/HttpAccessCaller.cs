using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Identity.Application.Abstractions;

namespace OpenPortal.Web.Authentication;

/// <summary>
/// Names the signed-in user to the Access module for what users do on their own behalf (asking for an
/// application, choosing an organization). Only a portal session counts: an application's own token is not a user.
/// </summary>
internal sealed class HttpAccessCaller : IAccessCaller
{
    private readonly ICurrentUser _currentUser;
    private readonly IApplicationApiCaller _application;

    public HttpAccessCaller(ICurrentUser currentUser, IApplicationApiCaller application)
    {
        _currentUser = currentUser;
        _application = application;
    }

    public Guid? UserId => _currentUser.IsAuthenticated && _application.ClientId is null ? _currentUser.UserId : null;
}
