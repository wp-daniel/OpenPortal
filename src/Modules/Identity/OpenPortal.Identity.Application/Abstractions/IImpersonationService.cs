using System.ComponentModel.DataAnnotations;
using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Identity.Application.Abstractions;

/// <summary>
/// Support sessions: an administrator signs in as an ordinary account to see what its owner sees. The session is
/// marked (who is behind it, since when), lasts at most <c>Identity:ImpersonationMinutes</c>, never opens the
/// applications or the account's sensitive settings, and every change made in it is audited with the administrator
/// as actor. The account's owner is told.
/// </summary>
public interface IImpersonationService
{
    /// <summary>
    /// Replaces the caller's session with one as <paramref name="userId"/>. Administrators only; never themselves,
    /// another administrator, or an account that cannot sign in (pending, locked out, waiting to be deleted).
    /// </summary>
    Task<Result> StartAsync(Guid userId, StartImpersonationRequest request, CancellationToken cancellationToken);

    /// <summary>Ends the support session and signs the administrator back in as themselves.</summary>
    Task<Result> StopAsync(CancellationToken cancellationToken);
}

/// <summary>Why the administrator needs to act as the account (a ticket number, a short explanation).</summary>
public sealed class StartImpersonationRequest
{
    public const int ReasonMaxLength = 200;

    [Required(AllowEmptyStrings = false, ErrorMessage = "validation.reason.required")]
    [StringLength(ReasonMaxLength, ErrorMessage = "validation.reason.max")]
    public string Reason { get; init; } = string.Empty;
}
