using OpenPortal.Identity.Domain.Users;

namespace OpenPortal.Tests.Unit.Identity;

public sealed class UserAvatarTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00];
    private static readonly byte[] Webp = [.. "RIFF"u8, 0x00, 0x00, 0x00, 0x00, .. "WEBP"u8, .. "VP8 "u8];

    [Theory]
    [InlineData("png", "image/png")]
    [InlineData("jpeg", "image/jpeg")]
    [InlineData("webp", "image/webp")]
    public void Create_derives_the_content_type_from_the_signature(string kind, string expected)
    {
        var content = kind switch { "png" => Png, "jpeg" => Jpeg, _ => Webp };

        var avatar = UserAvatar.Create(Guid.NewGuid(), content, Now);

        avatar.IsSuccess.ShouldBeTrue();
        avatar.Value.ContentType.ShouldBe(expected);
        avatar.Value.UpdatedAtUtc.ShouldBe(Now);
    }

    [Fact]
    public void Create_rejects_unrecognised_bytes()
    {
        // GIF and SVG are deliberately not accepted: SVG can carry script, and neither is needed for a photo.
        var avatar = UserAvatar.Create(Guid.NewGuid(), "<svg xmlns='http://www.w3.org/2000/svg'/>"u8.ToArray(), Now);

        avatar.IsFailure.ShouldBeTrue();
        avatar.Error.ShouldBe(UserErrors.AvatarUnsupportedType);
    }

    [Fact]
    public void Create_rejects_an_empty_or_oversized_file()
    {
        UserAvatar.Create(Guid.NewGuid(), [], Now).Error.ShouldBe(UserErrors.AvatarRequired);

        var oversized = new byte[UserAvatar.MaxBytes + 1];
        Png.CopyTo(oversized, 0);
        UserAvatar.Create(Guid.NewGuid(), oversized, Now).Error.ShouldBe(UserErrors.AvatarTooLarge);
    }

    [Fact]
    public void Replace_keeps_the_old_image_when_the_new_one_is_invalid()
    {
        var avatar = UserAvatar.Create(Guid.NewGuid(), Png, Now).Value;

        var replaced = avatar.Replace([0x00, 0x01], Now.AddMinutes(1));

        replaced.IsFailure.ShouldBeTrue();
        avatar.Content.ShouldBe(Png);
        avatar.UpdatedAtUtc.ShouldBe(Now);
    }
}
