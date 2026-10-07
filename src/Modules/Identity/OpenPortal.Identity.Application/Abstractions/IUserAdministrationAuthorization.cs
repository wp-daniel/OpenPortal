using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Identity.Application.Abstractions;

/// <summary>What the caller wants to do with other people's accounts, so the host can decide what allows it.</summary>
public enum UserAdministrationOperation
{
    /// <summary>Search the user list (also needed by screens that pick users, such as groups and access).</summary>
    ListUsers,

    /// <summary>Read, create and edit accounts, reset passwords and change pictures.</summary>
    ManageUsers,
}

/// <summary>
/// Decides whether the caller may administer accounts. Supplied by the host, which knows which portal pages the
/// caller holds; the module only adds its own rule that administrators can be changed by administrators alone.
/// </summary>
public interface IUserAdministrationAuthorization
{
    Task<Result> EnsureCanAsync(UserAdministrationOperation operation, CancellationToken cancellationToken);
}

/// <summary>
/// The portal pages a signed-in user may open, published in the session so the client can build its
/// navigation. Supplied by the host, which owns the pages and their grants.
/// </summary>
public interface IUserPageSource
{
    Task<IReadOnlyList<string>> GetPagesAsync(
        Guid userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken);
}
