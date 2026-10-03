using OpenPortal.Content.Application.Abstractions;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Domain.Users;
using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Web.Authorization;

/// <summary>
/// Answers the Content module's authorisation question using the host's authenticated session.
/// <para>
/// This is the adapter that lets <c>OpenPortal.Content</c> stay unaware of ASP.NET Core Identity: the module
/// declares that editing requires authorisation, and the host supplies the decision from its own session.
/// </para>
/// </summary>
internal sealed class RoleContentEditAuthorization : IContentEditAuthorization
{
    private readonly ICurrentUser _currentUser;

    public RoleContentEditAuthorization(ICurrentUser currentUser)
    {
        _currentUser = currentUser;
    }

    public Result EnsureCanEdit()
    {
        ArgumentNullException.ThrowIfNull(_currentUser);

        var isAdministrator = _currentUser.IsAuthenticated
            && _currentUser.Roles.Contains(Roles.Administrator, StringComparer.Ordinal);

        return isAdministrator
            ? Result.Success()
            : Result.Failure(Error.Forbidden(
                "content.edit_forbidden",
                "Editing site content requires an administrator account."));
    }
}