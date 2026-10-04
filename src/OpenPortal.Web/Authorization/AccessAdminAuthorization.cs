using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Access.Domain;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Domain.Users;
using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Web.Authorization;

/// <summary>
/// Answers the Access module's authorisation question from the host's session, as
/// <see cref="RoleContentEditAuthorization"/> does for Content.
/// </summary>
internal sealed class AccessAdminAuthorization : IAccessAdminAuthorization
{
    private readonly ICurrentUser _currentUser;

    public AccessAdminAuthorization(ICurrentUser currentUser)
    {
        _currentUser = currentUser;
    }

    public Result EnsureCanAdminister()
    {
        var isAdministrator = _currentUser.IsAuthenticated
            && _currentUser.Roles.Contains(Roles.Administrator, StringComparer.Ordinal);

        return isAdministrator ? Result.Success() : Result.Failure(AccessErrors.AdministrationForbidden);
    }
}
