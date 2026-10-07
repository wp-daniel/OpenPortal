using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using OpenPortal.Identity.Application.Abstractions;
using OpenPortal.Identity.Domain.Users;
using OpenPortal.SharedKernel.Results;
using OpenPortal.Web.Authorization;
using OpenPortal.Web.Infrastructure;

namespace OpenPortal.Web.Controllers;

/// <summary>
/// Profile pictures: read by any signed-in user, changed by the account itself or by an administrator.
/// <para>
/// Uploads are <c>multipart/form-data</c> with one <c>file</c> part. The size and the file signature are
/// checked by the domain (<see cref="UserAvatar"/>); the request limit here only stops an oversized body
/// from being buffered at all. Antiforgery applies as on every other unsafe request.
/// </para>
/// </summary>
[ApiController]
[Authorize]
public sealed class AvatarsController : ControllerBase
{
    /// <summary>Generous next to <see cref="UserAvatar.MaxBytes"/>, so the domain reports the precise error.</summary>
    private const long RequestLimitBytes = 1024 * 1024;

    private readonly IUserAvatarService _avatars;

    public AvatarsController(IUserAvatarService avatars)
    {
        _avatars = avatars;
    }

    /// <summary>
    /// Serves a user's picture. Clients version the URL with the account's <c>avatarUpdatedAtUtc</c>, so the
    /// response may be cached; the ETag still lets a revalidation end in a 304.
    /// </summary>
    [HttpGet("api/users/{userId:guid}/avatar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var avatar = await _avatars.GetAsync(userId, cancellationToken).ConfigureAwait(false);
        if (avatar.IsFailure)
        {
            return ProblemResults.FromResult(HttpContext, avatar);
        }

        var image = avatar.Value;
        Response.Headers.CacheControl = "private, max-age=86400";
        Response.Headers.XContentTypeOptions = "nosniff";

        return File(
            image.Content,
            image.ContentType,
            lastModified: image.UpdatedAtUtc,
            entityTag: new EntityTagHeaderValue($"\"{image.UpdatedAtUtc.UtcTicks}\""));
    }

    /// <summary>Replaces the signed-in account's picture.</summary>
    [HttpPut("api/account/avatar")]
    [RequestSizeLimit(RequestLimitBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = RequestLimitBytes)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetOwnAsync(IFormFile? file, CancellationToken cancellationToken)
    {
        var content = await ReadAsync(file, cancellationToken).ConfigureAwait(false);
        if (content.IsFailure)
        {
            return ProblemResults.FromResult(HttpContext, content);
        }

        var saved = await _avatars.SetOwnAsync(content.Value, cancellationToken).ConfigureAwait(false);

        return saved.IsSuccess ? NoContent() : ProblemResults.FromResult(HttpContext, saved);
    }

    /// <summary>Removes the signed-in account's picture.</summary>
    [HttpDelete("api/account/avatar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveOwnAsync(CancellationToken cancellationToken)
    {
        var removed = await _avatars.RemoveOwnAsync(cancellationToken).ConfigureAwait(false);

        return removed.IsSuccess ? NoContent() : ProblemResults.FromResult(HttpContext, removed);
    }

    /// <summary>Replaces another account's picture (administrators only).</summary>
    [HttpPut("api/admin/users/{userId:guid}/avatar")]
    [RequirePortalPage(PortalPages.Users)]
    [RequestSizeLimit(RequestLimitBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = RequestLimitBytes)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetForUserAsync(Guid userId, IFormFile? file, CancellationToken cancellationToken)
    {
        var content = await ReadAsync(file, cancellationToken).ConfigureAwait(false);
        if (content.IsFailure)
        {
            return ProblemResults.FromResult(HttpContext, content);
        }

        var saved = await _avatars.SetForUserAsync(userId, content.Value, cancellationToken).ConfigureAwait(false);

        return saved.IsSuccess ? NoContent() : ProblemResults.FromResult(HttpContext, saved);
    }

    /// <summary>Removes another account's picture (administrators only).</summary>
    [HttpDelete("api/admin/users/{userId:guid}/avatar")]
    [RequirePortalPage(PortalPages.Users)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var removed = await _avatars.RemoveForUserAsync(userId, cancellationToken).ConfigureAwait(false);

        return removed.IsSuccess ? NoContent() : ProblemResults.FromResult(HttpContext, removed);
    }

    /// <summary>
    /// Buffers the upload, refusing a missing or oversized file before reading it. The bytes are judged by
    /// the domain; the declared content type and file name are ignored on purpose.
    /// </summary>
    private static async Task<Result<byte[]>> ReadAsync(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return Result<byte[]>.Failure(UserErrors.AvatarRequired);
        }

        if (file.Length > UserAvatar.MaxBytes)
        {
            return Result<byte[]>.Failure(UserErrors.AvatarTooLarge);
        }

        using var buffer = new MemoryStream((int)file.Length);
        await file.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);

        return Result<byte[]>.Success(buffer.ToArray());
    }
}
