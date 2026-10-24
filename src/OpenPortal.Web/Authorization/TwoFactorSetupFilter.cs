using Microsoft.AspNetCore.Mvc.Filters;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Domain.Users;
using OpenPortal.SharedKernel.Results;
using OpenPortal.Web.Infrastructure;

namespace OpenPortal.Web.Authorization;

/// <summary>
/// Marks an endpoint an account may use while the portal still requires it to set up a second factor: the session,
/// signing out, the setup itself, languages and legal texts.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AllowDuringTwoFactorSetupAttribute : Attribute
{
}

/// <summary>
/// Enforces the portal's second-factor requirement: an account it covers that has neither an authenticator app nor a
/// passkey can sign in, but gets 403 <c>identity.two_factor_setup_required</c> from every endpoint not marked
/// <see cref="AllowDuringTwoFactorSetupAttribute"/>, until it sets one up. Support sessions are exempt (the
/// administrator behind them is not the account holder).
/// </summary>
public sealed class TwoFactorSetupFilter : IAsyncAuthorizationFilter, IOrderedFilter
{
    /// <summary>After authentication, before the action and its own checks.</summary>
    public int Order => 10;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.ActionDescriptor.EndpointMetadata.OfType<AllowDuringTwoFactorSetupAttribute>().Any())
        {
            return;
        }

        var currentUser = context.HttpContext.RequestServices.GetRequiredService<ICurrentUser>();
        if (currentUser.UserId is not { } userId || currentUser.ImpersonatorId is not null)
        {
            return;
        }

        var policy = context.HttpContext.RequestServices.GetRequiredService<ITwoFactorPolicy>();
        if (await policy.IsSetupRequiredAsync(userId, context.HttpContext.RequestAborted).ConfigureAwait(false))
        {
            context.Result = ProblemResults.FromResult(context.HttpContext, Result.Failure(UserErrors.TwoFactorSetupRequired));
        }
    }
}
