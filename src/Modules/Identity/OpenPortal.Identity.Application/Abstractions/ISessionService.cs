using OpenPortal.Identity.Application.Contracts;
using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Identity.Application.Abstractions;

/// <summary>
/// The portal's sessions, kept on the server: the signed-in account's own (scoped to <see cref="ICurrentUser"/>) and,
/// for whoever manages accounts, anyone's. Ending a session refuses its cookie from the next request on.
/// </summary>
public interface ISessionService
{
    Task<Result<AccountSessionsDto>> ListOwnAsync(CancellationToken cancellationToken);

    /// <summary>Ends one of the caller's other sessions (a browser left signed in somewhere).</summary>
    Task<Result> RevokeOwnAsync(string sessionId, CancellationToken cancellationToken);

    /// <summary>Ends every session of the caller except this one; the value is how many ended.</summary>
    Task<Result<int>> RevokeOtherOwnAsync(CancellationToken cancellationToken);

    Task<Result<AccountSessionsDto>> ListForUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<Result> RevokeForUserAsync(Guid userId, string sessionId, CancellationToken cancellationToken);
}
