using OpenPortal.Identity.Domain.Users;
using OpenPortal.SharedKernel.Auditing;

namespace OpenPortal.Identity.Infrastructure.Services;

/// <summary>How accounts are named in the audit log.</summary>
internal static class IdentityAudit
{
    /// <summary>The account by id, labelled with its email as it is now.</summary>
    public static AuditSubject ToAuditSubject(this ApplicationUser user) =>
        new(AuditSubjectTypes.User, user.Id.ToString(), user.Email);
}
