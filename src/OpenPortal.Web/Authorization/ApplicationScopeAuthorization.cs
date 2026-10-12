using Microsoft.AspNetCore.Authorization;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;
using OpenPortal.Web.Authentication;

namespace OpenPortal.Web.Authorization;

/// <summary>
/// Lets through an application calling with a token of its own (client credentials) that carries
/// <see cref="Scope"/>, or any such token when no scope is named. The bearer token is the only credential
/// read: the session cookie is ignored on these endpoints.
/// <para>
/// The first gate only. The Access and Identity services check the scope again, against the application as it is
/// stored now, so switching a scope off or disabling the application takes effect on the next request.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RequireApplicationScopeAttribute : AuthorizeAttribute, IAuthorizationRequirementData
{
    public RequireApplicationScopeAttribute(string? scope = null)
    {
        Scope = scope;
        AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
    }

    public string? Scope { get; }

    public IEnumerable<IAuthorizationRequirement> GetRequirements()
    {
        yield return new ApplicationScopeRequirement(Scope);
    }
}

public sealed record ApplicationScopeRequirement(string? Scope) : IAuthorizationRequirement;

internal sealed class ApplicationScopeAuthorizationHandler : AuthorizationHandler<ApplicationScopeRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ApplicationScopeRequirement requirement)
    {
        if (ApplicationTokens.IsApplication(context.User)
            && (requirement.Scope is null || context.User.HasScope(requirement.Scope)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
