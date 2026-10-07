using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Content.Application.Abstractions;

/// <summary>The parts of the content that are edited separately, so each can be delegated on its own.</summary>
public enum ContentArea
{
    Profile,
    Projects,
}

/// <summary>
/// Decides whether the caller may edit content.
/// <para>
/// This exists so that <c>OpenPortal.Content</c> stays independent of <c>OpenPortal.Identity</c>. The
/// Content module states the requirement ("someone must be authorised"); the host supplies the answer from
/// its own authentication stack. Without it, the module would either reference Identity directly or, worse,
/// take the decision on trust from the caller.
/// </para>
/// </summary>
public interface IContentEditAuthorization
{
    /// <summary>
    /// Returns <see cref="Result.Success"/> when the caller may edit <paramref name="area"/>, otherwise a
    /// failure describing why not.
    /// </summary>
    Task<Result> EnsureCanEditAsync(ContentArea area, CancellationToken cancellationToken);
}
