using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Access.Application.Contracts;
using OpenPortal.Access.Domain;
using OpenPortal.Access.Domain.Applications;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Application.Contracts;
using OpenPortal.SharedKernel.Results;
using OpenPortal.Web.Authorization;
using OpenPortal.Web.Infrastructure;

namespace OpenPortal.Web.Controllers;

/// <summary>
/// The application API: what an application's backend can do with a token of its own, without a user and without
/// the portal's screens. The token comes from <c>POST /connect/token</c> with <c>grant_type=client_credentials</c>
/// and the application's client id and secret, once an administrator has enabled API scopes for the application.
/// Everything here concerns the calling application only.
/// </summary>
[ApiController]
[Route("api/apps/me")]
[IgnoreAntiforgeryToken]
[Produces("application/json")]
public sealed class ApplicationApiController : ControllerBase
{
    private readonly IApplicationApiService _applications;
    private readonly IUserAdministrationService _users;
    private readonly IUserLookupService _lookup;

    public ApplicationApiController(
        IApplicationApiService applications,
        IUserAdministrationService users,
        IUserLookupService lookup)
    {
        _applications = applications;
        _users = users;
        _lookup = lookup;
    }

    /// <summary>The calling application: its roles, its groups claim setting and the API scopes enabled for it.</summary>
    [RequireApplicationScope]
    [HttpGet]
    [EndpointSummary("Get the calling application")]
    [ProducesResponseType<ApplicationSelfDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApplicationSelfDto>> GetAsync(CancellationToken cancellationToken)
    {
        var self = await _applications.GetSelfAsync(cancellationToken).ConfigureAwait(false);

        return self.IsSuccess ? Ok(self.Value) : ProblemResults.FromResult(HttpContext, self);
    }

    /// <summary>
    /// The users who can open the application, directly or through a group, with their roles in it and (as the
    /// application's groups claim setting allows) their groups. Ordered by name; <c>search</c> matches email and name.
    /// </summary>
    [RequireApplicationScope(ApplicationApiScopes.UsersRead)]
    [HttpGet("users")]
    [EndpointSummary("List the users who can open the application")]
    [ProducesResponseType<ApplicationUserPageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApplicationUserPageDto>> ListUsersAsync(
        [FromQuery] ApplicationUserListQuery query,
        CancellationToken cancellationToken)
    {
        var users = await _applications.ListUsersAsync(query, cancellationToken).ConfigureAwait(false);

        return users.IsSuccess ? Ok(users.Value) : ProblemResults.FromResult(HttpContext, users);
    }

    /// <summary>One user who can open the application. Not found when the account does not exist or has no access.</summary>
    [RequireApplicationScope(ApplicationApiScopes.UsersRead)]
    [HttpGet("users/{userId:guid}")]
    [EndpointSummary("Get a user who can open the application")]
    [ProducesResponseType<ApplicationUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApplicationUserDto>> GetUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _applications.GetUserAsync(userId, cancellationToken).ConfigureAwait(false);

        return user.IsSuccess ? Ok(user.Value) : ProblemResults.FromResult(HttpContext, user);
    }

    /// <summary>
    /// Adds a person to the application by email. An existing account is granted the application; a new one is
    /// created first (an ordinary account, never an administrator), which also needs <c>portal.users.create</c>
    /// plus <c>password</c>, <c>firstName</c> and <c>lastName</c>. The password must satisfy the portal's policy.
    /// <c>roles</c> are role keys of the application; absent, an existing grant keeps its roles.
    /// </summary>
    [RequireApplicationScope(ApplicationApiScopes.AccessManage)]
    [HttpPost("users")]
    [EndpointSummary("Add a user to the application, creating the account if needed")]
    [ProducesResponseType<AddApplicationUserResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AddApplicationUserResultDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AddApplicationUserResultDto>> AddUserAsync(
        [FromBody] AddApplicationUserRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var existing = await _lookup.FindByEmailAsync(request.Email, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            var granted = await _applications.GrantUserAsync(existing.Id, request.Roles, cancellationToken).ConfigureAwait(false);

            return granted.IsSuccess
                ? Ok(new AddApplicationUserResultDto(granted.Value.User, Created: false, granted.Value.Changed))
                : ProblemResults.FromResult(HttpContext, granted);
        }

        var account = new ProvisionUserRequest
        {
            Email = request.Email,
            Password = request.Password ?? string.Empty,
            FirstName = request.FirstName ?? string.Empty,
            LastName = request.LastName ?? string.Empty,
        };

        // The fields a new account needs are optional in the body, because adding an existing account does not
        // need them; check them now, with the same field errors the portal's own forms get.
        var problems = new List<ValidationResult>();
        if (!Validator.TryValidateObject(account, new ValidationContext(account), problems, validateAllProperties: true))
        {
            foreach (var problem in problems)
            {
                foreach (var member in problem.MemberNames)
                {
                    ModelState.AddModelError(char.ToLowerInvariant(member[0]) + member[1..], problem.ErrorMessage ?? string.Empty);
                }
            }

            return ValidationProblem(ModelState);
        }

        // Check the roles before creating anything, so a typo does not leave an account the application cannot use.
        var roles = await CheckRolesAsync(request.Roles, cancellationToken).ConfigureAwait(false);
        if (roles.IsFailure)
        {
            return ProblemResults.FromResult(HttpContext, roles);
        }

        var created = await _users.ProvisionUserAsync(account, cancellationToken).ConfigureAwait(false);
        if (created.IsFailure)
        {
            return ProblemResults.FromResult(HttpContext, created);
        }

        var grant = await _applications.GrantUserAsync(created.Value.Id, request.Roles, cancellationToken).ConfigureAwait(false);
        if (grant.IsFailure)
        {
            return ProblemResults.FromResult(HttpContext, grant);
        }

        return CreatedAtAction(
            nameof(GetUserAsync),
            new { userId = created.Value.Id },
            new AddApplicationUserResultDto(grant.Value.User, Created: true, Changed: true));
    }

    /// <summary>
    /// Grants the application to the user personally. With <c>roles</c> the grant carries exactly those role keys;
    /// without (or with no body), an existing grant keeps its roles and a new one has none.
    /// </summary>
    [RequireApplicationScope(ApplicationApiScopes.AccessManage)]
    [HttpPut("users/{userId:guid}/access")]
    [EndpointSummary("Grant the application to a user")]
    [ProducesResponseType<ApplicationGrantResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApplicationGrantResultDto>> GrantAsync(
        Guid userId,
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] GrantRequest? request,
        CancellationToken cancellationToken)
    {
        var granted = await _applications.GrantUserAsync(userId, request?.Roles, cancellationToken).ConfigureAwait(false);

        return granted.IsSuccess ? Ok(granted.Value) : ProblemResults.FromResult(HttpContext, granted);
    }

    /// <summary>
    /// Withdraws the user's personal grant and ends their sessions in the application, unless a group still grants
    /// it: <c>stillHasAccess</c> says so (group grants are managed in the portal).
    /// </summary>
    [RequireApplicationScope(ApplicationApiScopes.AccessManage)]
    [HttpDelete("users/{userId:guid}/access")]
    [EndpointSummary("Withdraw a user's personal access")]
    [ProducesResponseType<ApplicationRevokeResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApplicationRevokeResultDto>> RevokeAsync(Guid userId, CancellationToken cancellationToken)
    {
        var revoked = await _applications.RevokeUserAsync(userId, cancellationToken).ConfigureAwait(false);

        return revoked.IsSuccess ? Ok(revoked.Value) : ProblemResults.FromResult(HttpContext, revoked);
    }

    private async Task<Result> CheckRolesAsync(IReadOnlyList<string>? roles, CancellationToken cancellationToken)
    {
        if (roles is null || roles.Count == 0)
        {
            return Result.Success();
        }

        var self = await _applications.GetSelfAsync(cancellationToken).ConfigureAwait(false);
        if (self.IsFailure)
        {
            return Result.Failure(self.Error);
        }

        var defined = self.Value.Roles.Select(role => role.Key).ToHashSet(StringComparer.Ordinal);

        return roles.All(role => defined.Contains((role ?? string.Empty).Trim().ToLowerInvariant()))
            ? Result.Success()
            : Result.Failure(AccessErrors.RoleNotFound);
    }
}

/// <summary>A person to add to the calling application.</summary>
/// <remarks>
/// Only <see cref="Email"/> is needed for an existing account. A new account also needs <see cref="Password"/>,
/// <see cref="FirstName"/> and <see cref="LastName"/>, and the <c>portal.users.create</c> scope.
/// </remarks>
public sealed class AddApplicationUserRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "validation.email.required")]
    [EmailAddress(ErrorMessage = "validation.email.invalid")]
    [StringLength(256, ErrorMessage = "validation.email.max")]
    public string Email { get; init; } = string.Empty;

    [StringLength(256, ErrorMessage = "validation.password.max")]
    public string? Password { get; init; }

    [StringLength(OpenPortal.Identity.Domain.Users.ApplicationUser.NameMaxLength, ErrorMessage = "validation.name.max")]
    public string? FirstName { get; init; }

    [StringLength(OpenPortal.Identity.Domain.Users.ApplicationUser.NameMaxLength, ErrorMessage = "validation.name.max")]
    public string? LastName { get; init; }

    /// <summary>Role keys of the application to come with the grant; absent keeps an existing grant's roles.</summary>
    public IReadOnlyList<string>? Roles { get; init; }
}

/// <param name="Created">True when a new portal account was created.</param>
/// <param name="Changed">False when the account already held the grant with the same roles.</param>
public sealed record AddApplicationUserResultDto(ApplicationUserDto User, bool Created, bool Changed);
