using OpenPortal.SharedKernel.Results;

namespace OpenPortal.Identity.Domain.Users;

/// <summary>
/// A user's profile picture.
/// <para>
/// Kept out of <see cref="ApplicationUser"/> so that loading accounts (the user list, the session, every
/// <c>UserManager</c> call) never drags image bytes along. The account only carries
/// <see cref="ApplicationUser.AvatarUpdatedAtUtc"/>, which clients use to know a picture exists and to bust
/// their cache when it changes.
/// </para>
/// <para>
/// The content type is derived from the file signature, never taken from the upload: a client-declared type
/// would let any bytes be served back to other users' browsers as an image.
/// </para>
/// </summary>
public sealed class UserAvatar
{
    /// <summary>Upper bound on the stored image. Clients resize to a small square before uploading.</summary>
    public const int MaxBytes = 256 * 1024;

    public const int ContentTypeMaxLength = 32;

    /// <summary>Required by Entity Framework Core materialisation.</summary>
    private UserAvatar()
    {
    }

    private UserAvatar(Guid userId, byte[] content, string contentType, DateTimeOffset updatedAtUtc)
    {
        UserId = userId;
        Content = content;
        ContentType = contentType;
        UpdatedAtUtc = updatedAtUtc;
    }

    public Guid UserId { get; private set; }

    public byte[] Content { get; private set; } = [];

    public string ContentType { get; private set; } = string.Empty;

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static Result<UserAvatar> Create(Guid userId, byte[] content, DateTimeOffset now)
    {
        var contentType = Validate(content);

        return contentType.IsFailure
            ? Result<UserAvatar>.Failure(contentType.Error)
            : Result<UserAvatar>.Success(new UserAvatar(userId, content, contentType.Value, now));
    }

    public Result Replace(byte[] content, DateTimeOffset now)
    {
        var contentType = Validate(content);
        if (contentType.IsFailure)
        {
            return Result.Failure(contentType.Error);
        }

        Content = content;
        ContentType = contentType.Value;
        UpdatedAtUtc = now;

        return Result.Success();
    }

    /// <summary>Checks size and signature; returns the content type the signature identifies.</summary>
    private static Result<string> Validate(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (content.Length == 0)
        {
            return Result<string>.Failure(UserErrors.AvatarRequired);
        }

        if (content.Length > MaxBytes)
        {
            return Result<string>.Failure(UserErrors.AvatarTooLarge);
        }

        var contentType = DetectContentType(content);

        return contentType is null
            ? Result<string>.Failure(UserErrors.AvatarUnsupportedType)
            : Result<string>.Success(contentType);
    }

    /// <summary>PNG, JPEG or WebP by magic number; anything else is <see langword="null"/>.</summary>
    public static string? DetectContentType(ReadOnlySpan<byte> content)
    {
        ReadOnlySpan<byte> png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        ReadOnlySpan<byte> jpeg = [0xFF, 0xD8, 0xFF];
        ReadOnlySpan<byte> riff = "RIFF"u8;
        ReadOnlySpan<byte> webp = "WEBP"u8;

        if (content.StartsWith(png))
        {
            return "image/png";
        }

        if (content.StartsWith(jpeg))
        {
            return "image/jpeg";
        }

        if (content.Length >= 12 && content.StartsWith(riff) && content.Slice(8, 4).SequenceEqual(webp))
        {
            return "image/webp";
        }

        return null;
    }
}
