using Microsoft.AspNetCore.Mvc.Filters;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.SharedKernel.Results;
using OpenPortal.Web.Infrastructure;

namespace OpenPortal.Web.Authorization;

/// <summary>
/// Keeps an endpoint to the account holder's own browser session: refused (403
/// <c>auth.interactive_session_required</c>) to a personal access token and to an administrator impersonating the
/// account for support.
/// <para>
/// For what only the person themselves should do: change the password or the second factor, create tokens,
/// download or delete their data, or start an impersonation. A token stolen from a script, or a support session,
/// must never be able to take the account over or erase it.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class InteractiveSessionOnlyAttribute : Attribute, IAuthorizationFilter
{
    public static readonly Error Refused = Error.Forbidden(
        "auth.interactive_session_required",
        "This can only be done by the account holder, signed in to the portal with their password.");

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var currentUser = context.HttpContext.RequestServices.GetRequiredService<ICurrentUser>();

        if (currentUser.ImpersonatorId is not null || currentUser.PersonalAccessTokenId is not null)
        {
            context.Result = ProblemResults.FromResult(context.HttpContext, Result.Failure(Refused));
        }
    }
}
