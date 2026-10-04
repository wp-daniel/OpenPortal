using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenPortal.Identity.Domain.Users;

namespace OpenPortal.Identity.Infrastructure.Persistence;

/// <summary>
/// EF Core configuration for <see cref="UserAvatar"/>: one optional row per user, deleted with the user.
/// There is no navigation from <see cref="ApplicationUser"/>, so the image is only read when asked for.
/// </summary>
internal sealed class UserAvatarConfiguration : IEntityTypeConfiguration<UserAvatar>
{
    public void Configure(EntityTypeBuilder<UserAvatar> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("UserAvatars");

        builder.HasKey(avatar => avatar.UserId);

        builder.Property(avatar => avatar.Content)
            .IsRequired();

        builder.Property(avatar => avatar.ContentType)
            .HasMaxLength(UserAvatar.ContentTypeMaxLength)
            .IsRequired();

        builder.Property(avatar => avatar.UpdatedAtUtc)
            .IsRequired();

        builder.HasOne<ApplicationUser>()
            .WithOne()
            .HasForeignKey<UserAvatar>(avatar => avatar.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
