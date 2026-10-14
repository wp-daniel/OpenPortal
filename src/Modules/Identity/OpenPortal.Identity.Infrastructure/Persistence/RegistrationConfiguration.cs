using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using OpenPortal.Identity.Domain.Invitations;
using OpenPortal.Identity.Domain.Settings;

namespace OpenPortal.Identity.Infrastructure.Persistence;

/// <summary>
/// Short lists that are always read whole (allowed domains, the ids an invitation promises) are stored as one
/// newline-separated column rather than a child table; neither a host name nor a Guid contains a newline.
/// </summary>
internal static class ListColumns
{
    public static PropertyBuilder<IReadOnlyList<string>> AsStringList(this PropertyBuilder<IReadOnlyList<string>> property) =>
        property.IsRequired().HasConversion(
            new ValueConverter<IReadOnlyList<string>, string>(
                values => string.Join('\n', values),
                stored => stored.Split('\n', StringSplitOptions.RemoveEmptyEntries)),
            new ValueComparer<IReadOnlyList<string>>(
                (left, right) => (left ?? Array.Empty<string>()).SequenceEqual(right ?? Array.Empty<string>()),
                values => values.Aggregate(0, (hash, value) => HashCode.Combine(hash, value.GetHashCode(StringComparison.Ordinal))),
                values => values.ToArray()));

    public static PropertyBuilder<IReadOnlyList<Guid>> AsGuidList(this PropertyBuilder<IReadOnlyList<Guid>> property) =>
        property.IsRequired().HasConversion(
            new ValueConverter<IReadOnlyList<Guid>, string>(
                values => string.Join('\n', values),
                stored => stored.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(Guid.Parse).ToArray()),
            new ValueComparer<IReadOnlyList<Guid>>(
                (left, right) => (left ?? Array.Empty<Guid>()).SequenceEqual(right ?? Array.Empty<Guid>()),
                values => values.Aggregate(0, (hash, value) => HashCode.Combine(hash, value.GetHashCode())),
                values => values.ToArray()));
}

/// <summary>At most one row, keyed by <see cref="RegistrationSettings.SingletonId"/>. No row means the defaults.</summary>
internal sealed class RegistrationSettingsConfiguration : IEntityTypeConfiguration<RegistrationSettings>
{
    public void Configure(EntityTypeBuilder<RegistrationSettings> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("RegistrationSettings");

        builder.HasKey(settings => settings.Id);
        builder.Property(settings => settings.Id).ValueGeneratedNever();

        builder.Property(settings => settings.Mode)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(settings => settings.AllowedDomains).AsStringList();

        builder.Ignore(settings => settings.IsOpen);
    }
}

internal sealed class UserInvitationConfiguration : IEntityTypeConfiguration<UserInvitation>
{
    public void Configure(EntityTypeBuilder<UserInvitation> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Invitations");

        builder.HasKey(invitation => invitation.Id);

        builder.Property(invitation => invitation.Email).IsRequired().HasMaxLength(UserInvitation.EmailMaxLength);
        builder.Property(invitation => invitation.NormalizedEmail).IsRequired().HasMaxLength(UserInvitation.EmailMaxLength);
        builder.Property(invitation => invitation.FirstName).HasMaxLength(Domain.Users.ApplicationUser.NameMaxLength);
        builder.Property(invitation => invitation.LastName).HasMaxLength(Domain.Users.ApplicationUser.NameMaxLength);
        builder.Property(invitation => invitation.Message).HasMaxLength(UserInvitation.MessageMaxLength);
        builder.Property(invitation => invitation.TokenHash).IsRequired().HasMaxLength(UserInvitation.TokenHashMaxLength);
        builder.Property(invitation => invitation.GroupIds).AsGuidList();
        builder.Property(invitation => invitation.OrganizationIds).AsGuidList();

        // Accepting looks the token up; "is one waiting for this address" looks the email up.
        builder.HasIndex(invitation => invitation.TokenHash)
            .IsUnique()
            .HasDatabaseName("IX_Invitations_TokenHash");

        builder.HasIndex(invitation => invitation.NormalizedEmail)
            .HasDatabaseName("IX_Invitations_NormalizedEmail");

        builder.HasIndex(invitation => invitation.CreatedAtUtc)
            .HasDatabaseName("IX_Invitations_CreatedAtUtc");
    }
}
