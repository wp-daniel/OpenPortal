using OpenPortal.Identity.Application.Contracts;
using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Identity.Application.Abstractions;

/// <summary>
/// Profile pictures.
/// <para>
/// Any signed-in user may read a picture (they appear wherever a person is shown). Changing one is limited to
/// the account itself (<c>*Own*</c>) or an administrator; the administrator methods re-check the caller's role
/// like every other administrative operation.
/// </para>
/// </summary>
public interface IUserAvatarService
{
    Task<Result<AvatarImageDto>> GetAsync(Guid userId, CancellationToken cancellationToken);

    Task<Result> SetOwnAsync(byte[] content, CancellationToken cancellationToken);

    Task<Result> RemoveOwnAsync(CancellationToken cancellationToken);

    Task<Result> SetForUserAsync(Guid userId, byte[] content, CancellationToken cancellationToken);

    Task<Result> RemoveForUserAsync(Guid userId, CancellationToken cancellationToken);
}
