using OpenPortal.Access.Application.Abstractions;
using OpenPortal.Access.Domain.Applications;
using OpenPortal.Access.Domain.Groups;
using OpenPortal.SharedKernel.Auditing;

namespace OpenPortal.Access.Infrastructure.Services;

/// <summary>How the Access module names things in the audit log.</summary>
internal static class AccessAudit
{
    public static AuditSubject ToAuditSubject(this PortalApplication application) =>
        new(AuditSubjectTypes.Application, application.ClientId, application.DisplayName);

    public static AuditSubject ToAuditSubject(this Group group) =>
        new(AuditSubjectTypes.Group, group.Id.ToString(), group.Name);

    /// <summary>A user by id, labelled with their email when the account still exists.</summary>
    public static async Task<AuditSubject> UserSubjectAsync(
        this IUserDirectory directory,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var found = await directory.FindAsync([userId], cancellationToken).ConfigureAwait(false);

        return new AuditSubject(
            AuditSubjectTypes.User,
            userId.ToString(),
            found.TryGetValue(userId, out var user) ? user.Email : null);
    }

    /// <summary>Details naming the other side of a grant or membership, and the roles that came with it.</summary>
    public static Dictionary<string, string?> Details(params (string Key, string? Value)[] pairs) =>
        pairs.Where(pair => pair.Value is not null).ToDictionary(pair => pair.Key, pair => pair.Value);

    public static string? JoinRoles(IReadOnlyCollection<string>? roles) =>
        roles is null ? null : string.Join(", ", roles);
}
