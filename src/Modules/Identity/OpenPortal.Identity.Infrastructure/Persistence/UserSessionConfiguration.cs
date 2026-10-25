using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenPortal.Identity.Domain.Users;

namespace OpenPortal.Identity.Infrastructure.Persistence;

/// <summary>Sessions are looked up by id on every request, listed per account, and swept by age.</summary>
internal sealed class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("UserSessions");

        builder.HasKey(session => session.Id);
        builder.Property(session => session.Id).HasMaxLength(UserSession.IdLength).ValueGeneratedNever();

        builder.Property(session => session.IpAddress).HasMaxLength(UserSession.IpAddressMaxLength);
        builder.Property(session => session.UserAgent).HasMaxLength(UserSession.UserAgentMaxLength);
        builder.Property(session => session.Device).HasMaxLength(UserSession.DeviceMaxLength);
        builder.Property(session => session.Methods).HasMaxLength(UserSession.MethodsMaxLength);
        builder.Property(session => session.Ticket).IsRequired();

        builder.Property(session => session.EndReason)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(16);

        // The account's sessions go with the account.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(session => session.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(session => new { session.UserId, session.CreatedAtUtc })
            .HasDatabaseName("IX_UserSessions_UserId_CreatedAtUtc");

        builder.HasIndex(session => session.LastSeenAtUtc)
            .HasDatabaseName("IX_UserSessions_LastSeenAtUtc");
    }
}
