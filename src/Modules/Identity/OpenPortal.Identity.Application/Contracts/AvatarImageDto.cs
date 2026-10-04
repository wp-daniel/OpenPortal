namespace OpenPortal.Identity.Application.Contracts;

/// <summary>A stored profile picture, ready to be served.</summary>
/// <param name="ContentType">Derived from the file signature on upload, never from the client.</param>
public sealed record AvatarImageDto(byte[] Content, string ContentType, DateTimeOffset UpdatedAtUtc);
