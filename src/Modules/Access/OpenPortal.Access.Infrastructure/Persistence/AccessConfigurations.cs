using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using OpenPortal.Access.Domain.Applications;
using OpenPortal.Access.Domain.Grants;
using OpenPortal.Access.Domain.Groups;
using OpenPortal.Access.Domain.Organizations;
using OpenPortal.Access.Domain.Requests;

namespace OpenPortal.Access.Infrastructure.Persistence;

/// <summary>
/// Short string lists that are always read whole (redirect URIs, role keys) are stored as one newline-separated
/// column rather than a child table. Neither a URI nor a role key can contain a raw newline, which makes the
/// separator unambiguous.
/// </summary>
internal static class StringListConversion
{
    public static readonly ValueConverter<IReadOnlyList<string>, string> Converter = new(
        values => string.Join('\n', values),
        stored => stored.Split('\n', StringSplitOptions.RemoveEmptyEntries));

    public static readonly ValueComparer<IReadOnlyList<string>> Comparer = new(
        (left, right) => (left ?? Array.Empty<string>()).SequenceEqual(right ?? Array.Empty<string>()),
        values => values.Aggregate(0, (hash, value) => HashCode.Combine(hash, value.GetHashCode(StringComparison.Ordinal))),
        values => values.ToArray());

    public static PropertyBuilder<IReadOnlyList<string>> AsStringList(this PropertyBuilder<IReadOnlyList<string>> property) =>
        property.IsRequired().HasConversion(Converter, Comparer);
}

internal sealed class PortalApplicationConfiguration : IEntityTypeConfiguration<PortalApplication>
{
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

        // Existing rows are confidential clients (the only kind there was).
        builder.Property(application => application.ClientType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(16)
            .HasDefaultValue(ApplicationClientType.Confidential)
            .HasSentinel(ApplicationClientType.Confidential);

        builder.Property(application => application.BackChannelLogoutUri)
            .HasMaxLength(PortalApplication.UrlMaxLength);

        builder.Property(application => application.AnnouncedBackChannelLogoutUri)
            .HasMaxLength(PortalApplication.UrlMaxLength);

        builder.Property(application => application.WebhookUrl)
            .HasMaxLength(PortalApplication.UrlMaxLength);

        builder.Property(application => application.ProtectedWebhookSecret)
            .HasMaxLength(2000);

        foreach (var list in new[]
                 {
                     nameof(PortalApplication.RedirectUris),
                     nameof(PortalApplication.PostLogoutRedirectUris),
                     nameof(PortalApplication.AnnouncedRedirectUris),
                     nameof(PortalApplication.AnnouncedPostLogoutRedirectUris),
                     nameof(PortalApplication.ApiScopes),
                     nameof(PortalApplication.WebhookEvents),
                 })
        {
            builder.Property<IReadOnlyList<string>>(list).AsStringList();
        }

        builder.Property(application => application.GroupClaims)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(16);

        // Existing rows take the member name, not "", so they read back as None.
        builder.Property(application => application.Organizations)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(16)
            .HasDefaultValue(OrganizationMode.None)
            .HasSentinel(OrganizationMode.None);

        builder.Ignore(application => application.HasManifestChanges);
        builder.Ignore(application => application.IsPublic);

        builder.HasMany(application => application.Roles)
            .WithOne()
            .HasForeignKey(role => role.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(PortalApplication.Roles))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // Roles are few and needed wherever an application is shown or a token is issued.
        builder.Navigation(application => application.Roles).AutoInclude();

        // The client id is what OpenIddict looks clients up by, and what a deployed application is configured
        // with. The domain lower-cases it, so a plain unique index is case-insensitive in effect.
        builder.HasIndex(application => application.ClientId)
            .IsUnique()
            .HasDatabaseName("IX_Applications_ClientId");
    }
}

internal sealed class ApplicationRoleConfiguration : IEntityTypeConfiguration<ApplicationRole>
{
    public void Configure(EntityTypeBuilder<ApplicationRole> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ApplicationRoles");

        builder.HasKey(role => new { role.ApplicationId, role.Key });

        builder.Property(role => role.Key).IsRequired().HasMaxLength(ApplicationRole.KeyMaxLength);
        builder.Property(role => role.DisplayName).IsRequired().HasMaxLength(ApplicationRole.DisplayNameMaxLength);
        builder.Property(role => role.Description).HasMaxLength(ApplicationRole.DescriptionMaxLength);
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

        // The expiry sweep asks for what ended.
        builder.HasIndex(member => member.ExpiresAtUtc)
            .HasDatabaseName("IX_GroupMembers_ExpiresAtUtc");
    }
}

internal sealed class GroupNestingConfiguration : IEntityTypeConfiguration<GroupNesting>
{
    public void Configure(EntityTypeBuilder<GroupNesting> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("GroupNestings");

        builder.HasKey(nesting => new { nesting.ParentGroupId, nesting.ChildGroupId });

        // Deleting either group removes the nesting.
        builder.HasOne<Group>().WithMany().HasForeignKey(nesting => nesting.ParentGroupId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Group>().WithMany().HasForeignKey(nesting => nesting.ChildGroupId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(nesting => nesting.ChildGroupId)
            .HasDatabaseName("IX_GroupNestings_ChildGroupId");
    }
}

internal sealed class ApplicationUserGrantConfiguration : IEntityTypeConfiguration<ApplicationUserGrant>
{
    public void Configure(EntityTypeBuilder<ApplicationUserGrant> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ApplicationUserGrants");

        builder.HasKey(grant => new { grant.ApplicationId, grant.UserId });

        builder.Property(grant => grant.Roles).AsStringList();

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

        builder.Property(grant => grant.Roles).AsStringList();

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

internal sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Organizations");

        builder.HasKey(organization => organization.Id);

        builder.Property(organization => organization.Name).IsRequired().HasMaxLength(Organization.NameMaxLength);
        builder.Property(organization => organization.Slug).IsRequired().HasMaxLength(Organization.SlugMaxLength);
        builder.Property(organization => organization.Description).HasMaxLength(Organization.DescriptionMaxLength);

        builder.HasMany(organization => organization.Members)
            .WithOne()
            .HasForeignKey(member => member.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(Organization.Members))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // The domain lower-cases the slug, so a plain unique index is case-insensitive in effect.
        builder.HasIndex(organization => organization.Slug)
            .IsUnique()
            .HasDatabaseName("IX_Organizations_Slug");
    }
}

internal sealed class OrganizationMemberConfiguration : IEntityTypeConfiguration<OrganizationMember>
{
    public void Configure(EntityTypeBuilder<OrganizationMember> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("OrganizationMembers");

        builder.HasKey(member => new { member.OrganizationId, member.UserId });

        builder.Property(member => member.Role)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(16);

        // "Which organizations is this user in" is asked on every sign-in to an application that uses them.
        builder.HasIndex(member => member.UserId)
            .HasDatabaseName("IX_OrganizationMembers_UserId");
    }
}

/// <summary>A member's roles in one application within one organization; they go with the membership and the application.</summary>
internal sealed class OrganizationMemberRoleConfiguration : IEntityTypeConfiguration<OrganizationMemberRole>
{
    public void Configure(EntityTypeBuilder<OrganizationMemberRole> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("OrganizationMemberRoles");

        builder.HasKey(role => new { role.OrganizationId, role.UserId, role.ApplicationId, role.Role });
        builder.Property(role => role.Role).HasMaxLength(ApplicationRole.KeyMaxLength);

        builder.HasOne<OrganizationMember>()
            .WithMany()
            .HasForeignKey(role => new { role.OrganizationId, role.UserId })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<PortalApplication>()
            .WithMany()
            .HasForeignKey(role => role.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        // The token endpoint asks for one user's roles in one application.
        builder.HasIndex(role => new { role.UserId, role.ApplicationId })
            .HasDatabaseName("IX_OrganizationMemberRoles_UserId_ApplicationId");
    }
}

internal sealed class OrganizationDomainConfiguration : IEntityTypeConfiguration<OrganizationDomain>
{
    public void Configure(EntityTypeBuilder<OrganizationDomain> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("OrganizationDomains");

        builder.HasKey(domain => domain.Id);
        builder.Property(domain => domain.Domain).IsRequired().HasMaxLength(OrganizationDomain.DomainMaxLength);
        builder.Property(domain => domain.VerificationToken).IsRequired().HasMaxLength(OrganizationDomain.TokenLength);
        builder.Property(domain => domain.VerifiedBy).HasMaxLength(16);
        builder.Property(domain => domain.AutoJoinRole).IsRequired().HasConversion<string>().HasMaxLength(16);
        builder.Ignore(domain => domain.IsVerified);
        builder.Ignore(domain => domain.RecordValue);

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(domain => domain.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        // One organization per domain: two could not both prove they own it.
        builder.HasIndex(domain => domain.Domain)
            .IsUnique()
            .HasDatabaseName("IX_OrganizationDomains_Domain");
    }
}

internal sealed class AccessRequestConfiguration : IEntityTypeConfiguration<AccessRequest>
{
    public void Configure(EntityTypeBuilder<AccessRequest> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AccessRequests");

        builder.HasKey(request => request.Id);

        builder.Property(request => request.Reason).HasMaxLength(AccessRequest.ReasonMaxLength);
        builder.Property(request => request.DecisionNote).HasMaxLength(AccessRequest.NoteMaxLength);

        builder.Property(request => request.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(request => request.GrantedRoles).AsStringList();

        builder.Ignore(request => request.IsPending);

        // Deleting an application takes its requests with it, as it takes its grants.
        builder.HasOne<PortalApplication>()
            .WithMany()
            .HasForeignKey(request => request.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        // "My requests" and "is one pending already" look the user up; the reviewers' list goes by status.
        builder.HasIndex(request => new { request.UserId, request.ApplicationId })
            .HasDatabaseName("IX_AccessRequests_UserId_ApplicationId");

        builder.HasIndex(request => new { request.Status, request.CreatedAtUtc })
            .HasDatabaseName("IX_AccessRequests_Status_CreatedAtUtc");

        builder.HasIndex(request => request.ApplicationId)
            .HasDatabaseName("IX_AccessRequests_ApplicationId");
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

internal sealed class AccessReviewConfiguration : IEntityTypeConfiguration<Domain.Reviews.AccessReview>
{
    public void Configure(EntityTypeBuilder<Domain.Reviews.AccessReview> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AccessReviews");
        builder.HasKey(review => review.Id);

        builder.Property(review => review.Name).IsRequired().HasMaxLength(Domain.Reviews.AccessReview.NameMaxLength);
        builder.Property(review => review.Status).IsRequired().HasConversion<string>().HasMaxLength(16);

        // A review outlives nothing: deleting the application deletes its reviews.
        builder.HasOne<PortalApplication>().WithMany().HasForeignKey(review => review.ApplicationId).OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(review => review.Items).WithOne().HasForeignKey(item => item.ReviewId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(review => review.Items).HasField("_items").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class AccessReviewItemConfiguration : IEntityTypeConfiguration<Domain.Reviews.AccessReviewItem>
{
    public void Configure(EntityTypeBuilder<Domain.Reviews.AccessReviewItem> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AccessReviewItems");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.SubjectType).IsRequired().HasConversion<string>().HasMaxLength(16);
        builder.Property(item => item.SubjectLabel).IsRequired().HasMaxLength(Domain.Reviews.AccessReviewItem.LabelMaxLength);
        builder.Property(item => item.Decision).IsRequired().HasConversion<string>().HasMaxLength(16);
        builder.Property(item => item.Note).HasMaxLength(Domain.Reviews.AccessReview.NoteMaxLength);
        builder.Property<IReadOnlyList<string>>(nameof(Domain.Reviews.AccessReviewItem.Roles)).AsStringList();
    }
}
