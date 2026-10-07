using Microsoft.AspNetCore.Authorization;
using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Domain.Users;

namespace OpenPortal.Web.Authorization;

/// <summary>
/// Lets the caller through when they hold at least one of <see cref="Pages"/> (administrators hold them all).
/// <para>
/// Several pages are listed when one page's screen reads another's data, for example the groups page picks
/// users, so listing users is allowed to holders of the users, groups or access page.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RequirePortalPageAttribute : AuthorizeAttribute, IAuthorizationRequirementData
{
    public RequirePortalPageAttribute(params string[] pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentOutOfRangeException.ThrowIfZero(pages.Length);

        Pages = pages;
    }

    public IReadOnlyList<string> Pages { get; }

    public IEnumerable<IAuthorizationRequirement> GetRequirements()
    {
        yield return new PortalPageRequirement(Pages);
    }
}

public sealed record PortalPageRequirement(IReadOnlyList<string> Pages) : IAuthorizationRequirement;

/// <summary>Answers <see cref="PortalPageRequirement"/> from the caller's pages, read fresh on every request.</summary>
internal sealed class PortalPageAuthorizationHandler : AuthorizationHandler<PortalPageRequirement>
{
    private readonly CurrentPagePermissions _permissions;

    public PortalPageAuthorizationHandler(CurrentPagePermissions permissions)
    {
        _permissions = permissions;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PortalPageRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        if (await _permissions.HoldsAnyAsync(requirement.Pages, CancellationToken.None).ConfigureAwait(false))
        {
            context.Succeed(requirement);
        }
    }
}

/// <summary>
/// The pages the current caller may open, loaded once per request.
/// <para>
/// Nothing is stored in the cookie: the grants are read from the database, so removing a group's page or a
/// user's membership takes effect on the very next request.
/// </para>
/// </summary>
internal sealed class CurrentPagePermissions
{
    private readonly ICurrentUser _currentUser;
    private readonly IPagePermissionEvaluator _evaluator;
    private IReadOnlyList<string>? _pages;

    public CurrentPagePermissions(ICurrentUser currentUser, IPagePermissionEvaluator evaluator)
    {
        _currentUser = currentUser;
        _evaluator = evaluator;
    }

    public bool IsAdministrator =>
        _currentUser.IsAuthenticated && _currentUser.Roles.Contains(Roles.Administrator, StringComparer.Ordinal);

    public async Task<bool> HoldsAnyAsync(IEnumerable<string> pages, CancellationToken cancellationToken)
    {
        if (IsAdministrator)
        {
            return true;
        }

        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not Guid userId)
        {
            return false;
        }

        _pages ??= await _evaluator.GetPagesAsync(userId, cancellationToken).ConfigureAwait(false);

        return pages.Any(page => _pages.Contains(page, StringComparer.Ordinal));
    }
}
