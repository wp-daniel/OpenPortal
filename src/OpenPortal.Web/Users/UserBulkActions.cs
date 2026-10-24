using System.ComponentModel.DataAnnotations;
using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Application.Contracts;
using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Web.Users;

/// <summary>One action on several accounts at once, from the selection in the user list.</summary>
public sealed class UserBulkRequest
{
    /// <summary>One of <see cref="UserBulkActionNames"/>.</summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "validation.required")]
    public string Action { get; init; } = string.Empty;

    [Required(ErrorMessage = "validation.required")]
    [MinLength(1, ErrorMessage = "validation.selection.max")]
    [MaxLength(UserBulkLimits.MaxUsers, ErrorMessage = "validation.selection.max")]
    public IReadOnlyList<Guid> UserIds { get; init; } = [];

    /// <summary>The group, for <c>add_to_group</c> and <c>remove_from_group</c>.</summary>
    public Guid? GroupId { get; init; }
}

/// <summary>What became of one account.</summary>
public sealed record UserBulkResultDto(Guid UserId, bool Succeeded, string? ErrorCode);

public static class UserBulkActionNames
{
    public const string Approve = "approve";
    public const string AddToGroup = "add_to_group";
    public const string RemoveFromGroup = "remove_from_group";
    public const string SignOut = "sign_out";
    public const string Delete = "delete";
}

public static class UserBulkErrors
{
    public static readonly Error ActionInvalid =
        Error.Validation("users.bulk_action_invalid", "The action must be approve, add_to_group, remove_from_group, sign_out or delete.");

    public static readonly Error GroupRequired =
        Error.Validation("users.bulk_group_required", "Choose the group to add the accounts to or remove them from.");
}

/// <summary>
/// Runs one action on each selected account through the same services as the single-account screens, so every
/// check (page, administrators, yourself) applies per account; one refusal does not stop the others.
/// </summary>
public sealed class UserBulkActions
{
    private readonly IUserAdministrationService _users;
    private readonly IGroupService _groups;
    private readonly IAccessUserMaintenance _access;
    private readonly AccountEraser _eraser;
    private readonly OpenPortal.Web.Oidc.BackChannelLogoutNotifier _logout;

    public UserBulkActions(
        IUserAdministrationService users,
        IGroupService groups,
        IAccessUserMaintenance access,
        AccountEraser eraser,
        OpenPortal.Web.Oidc.BackChannelLogoutNotifier logout)
    {
        _logout = logout;
        _users = users;
        _groups = groups;
        _access = access;
        _eraser = eraser;
    }

    public async Task<Result<IReadOnlyList<UserBulkResultDto>>> RunAsync(UserBulkRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var action = request.Action?.Trim().ToLowerInvariant();
        Func<Guid, Task<Result>>? run = action switch
        {
            UserBulkActionNames.Approve => async userId => await _users.ApproveRegistrationAsync(userId, cancellationToken).ConfigureAwait(false),
            UserBulkActionNames.Delete => userId => _eraser.DeleteAsync(userId, cancellationToken),
            UserBulkActionNames.SignOut => SignOutAsync,
            UserBulkActionNames.AddToGroup when request.GroupId is { } groupId =>
                async userId => await _groups.AddMemberAsync(groupId, userId, cancellationToken).ConfigureAwait(false),
            UserBulkActionNames.RemoveFromGroup when request.GroupId is { } groupId =>
                async userId => await _groups.RemoveMemberAsync(groupId, userId, cancellationToken).ConfigureAwait(false),
            _ => null,
        };

        if (run is null)
        {
            return Result<IReadOnlyList<UserBulkResultDto>>.Failure(
                action is UserBulkActionNames.AddToGroup or UserBulkActionNames.RemoveFromGroup
                    ? UserBulkErrors.GroupRequired
                    : UserBulkErrors.ActionInvalid);
        }

        var results = new List<UserBulkResultDto>();
        foreach (var userId in request.UserIds.Distinct().Take(UserBulkLimits.MaxUsers))
        {
            var result = await run(userId).ConfigureAwait(false);
            results.Add(new UserBulkResultDto(userId, result.IsSuccess, result.IsSuccess ? null : result.Error.Code));
        }

        return Result<IReadOnlyList<UserBulkResultDto>>.Success(results);

        async Task<Result> SignOutAsync(Guid userId)
        {
            var revoked = await _users.RevokeSessionsAsync(userId, cancellationToken).ConfigureAwait(false);
            if (revoked.IsSuccess)
            {
                // The applications are told before the authorizations that name them are revoked.
                await _logout.NotifyAsync(userId, cancellationToken).ConfigureAwait(false);
                await _access.RevokeTokensAsync(userId, cancellationToken).ConfigureAwait(false);
            }

            return revoked;
        }
    }
}
