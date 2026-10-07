using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using OpenPortal.Access.Domain.Applications;
using OpenPortal.Access.Domain.Grants;
using OpenPortal.Access.Domain.Groups;

namespace OpenPortal.Access.Infrastructure.Persistence;

internal sealed class PortalApplicationConfiguration : IEntityTypeConfiguration<PortalApplication>
{
    // Redirect URI lists are small and always read whole, so they are stored as one newline-separated column
    // rather than a child table. A URI cannot contain a raw newline, which makes the separator unambiguous.
    private static readonly ValueConverter<IReadOnlyList<string>, string> UriListConverter = new(
        uris => string.Join('\n', uris),
        stored => stored.Split('\n', StringSplitOptions.RemoveEmptyEntries));

    private static readonly ValueComparer<IReadOnlyList<string>> UriListComparer = new(
        (left, right) => (left ?? Array.Empty<string>()).SequenceEqual(right ?? Array.Empty<string>()),
        uris => uris.Aggregate(0, (hash, uri) => HashCode.Combine(hash, uri.GetHashCode(StringComparison.Ordinal))),
        uris => uris.ToArray());

    public void Configure(EntityTypeBuilder<PortalApplication> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Applications");

        builder.HasKey(application => application.Id);

        builder.Property(application => application.ClientId)
            .IsRequired()
            .HasMaxLength(PortalApplication.ClientIdMaxLength);

        builder.Property(application => application.DisplayName)
            .IsRequired()
            .HasMaxLength(PortalApplication.DisplayNameMaxLength);

        builder.Property(application => application.Description)
            .HasMaxLength(PortalApplication.DescriptionMaxLength);

        builder.Property(application => application.BaseUrl)
            .IsRequired()
            .HasMaxLength(PortalApplication.UrlMaxLength);

        builder.Property(application => application.Version)
            .HasMaxLength(PortalApplication.VersionMaxLength);

        builder.Property(application => application.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(application => application.Source)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(16);

        foreach (var list in new[]
                 {
                     nameof(PortalApplication.RedirectUris),
                     nameof(PortalApplication.PostLogoutRedirectUris),
                     nameof(PortalApplication.AnnouncedRedirectUris),
                     nameof(PortalApplication.AnnouncedPostLogoutRedirectUris),
                 })
        {
            builder.Property<IReadOnlyList<string>>(list)
                .IsRequired()
                .HasConversion(UriListConverter, UriListComparer);
        }

        builder.Ignore(application => application.HasManifestChanges);

        // The client id is what OpenIddict looks clients up by, and what a deployed application is configured
        // with. The domain lower-cases it, so a plain unique index is case-insensitive in effect.
        builder.HasIndex(application => application.ClientId)
            .IsUnique()
            .HasDatabaseName("IX_Applications_ClientId");
    }
}

internal sealed class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Groups");

        builder.HasKey(group => group.Id);

        builder.Property(group => group.Name)
            .IsRequired()
            .HasMaxLength(Group.NameMaxLength);

        builder.Property(group => group.NormalisedName)
            .IsRequired()
            .HasMaxLength(Group.NameMaxLength);

        builder.Property(group => group.Description)
            .HasMaxLength(Group.DescriptionMaxLength);

        builder.HasMany(group => group.Members)
            .WithOne()
            .HasForeignKey(member => member.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(Group.Members))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(group => group.NormalisedName)
            .IsUnique()
            .HasDatabaseName("IX_Groups_NormalisedName");
    }
}

internal sealed class GroupMemberConfiguration : IEntityTypeConfiguration<GroupMember>
{
    public void Configure(EntityTypeBuilder<GroupMember> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("GroupMembers");

        builder.HasKey(member => new { member.GroupId, member.UserId });

        // "Which groups is this user in" is asked on every sign-in.
        builder.HasIndex(member => member.UserId)
            .HasDatabaseName("IX_GroupMembers_UserId");
    }
}

internal sealed class ApplicationUserGrantConfiguration : IEntityTypeConfiguration<ApplicationUserGrant>
{
    public void Configure(EntityTypeBuilder<ApplicationUserGrant> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ApplicationUserGrants");

        builder.HasKey(grant => new { grant.ApplicationId, grant.UserId });

        builder.HasOne<PortalApplication>()
            .WithMany()
            .HasForeignKey(grant => grant.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(grant => grant.UserId)
            .HasDatabaseName("IX_ApplicationUserGrants_UserId");
    }
}

internal sealed class ApplicationGroupGrantConfiguration : IEntityTypeConfiguration<ApplicationGroupGrant>
{
    public void Configure(EntityTypeBuilder<ApplicationGroupGrant> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ApplicationGroupGrants");

        builder.HasKey(grant => new { grant.ApplicationId, grant.GroupId });

        builder.HasOne<PortalApplication>()
            .WithMany()
            .HasForeignKey(grant => grant.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Group>()
            .WithMany()
            .HasForeignKey(grant => grant.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(grant => grant.GroupId)
            .HasDatabaseName("IX_ApplicationGroupGrants_GroupId");
    }
}

internal sealed class PageGroupGrantConfiguration : IEntityTypeConfiguration<PageGroupGrant>
{
    public void Configure(EntityTypeBuilder<PageGroupGrant> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("PageGroupGrants");

        builder.HasKey(grant => new { grant.PageKey, grant.GroupId });

        builder.Property(grant => grant.PageKey)
            .IsRequired()
            .HasMaxLength(PageGroupGrant.PageKeyMaxLength);

        builder.HasOne<Group>()
            .WithMany()
            .HasForeignKey(grant => grant.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        // "Which pages may this user open" joins memberships to grants by group on every admin request.
        builder.HasIndex(grant => grant.GroupId)
            .HasDatabaseName("IX_PageGroupGrants_GroupId");
    }
}
