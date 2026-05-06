using backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Data.Configurations;

public class HouseholdInvitationConfiguration : IEntityTypeConfiguration<HouseholdInvitation>
{
    public void Configure(EntityTypeBuilder<HouseholdInvitation> builder)
    {
        builder.HasOne(i => i.Household)
            .WithMany(h => h.Invitations)
            .HasForeignKey(i => i.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.Inviter)
            .WithMany(u => u.SentInvitations)
            .HasForeignKey(i => i.InviterId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(i => i.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()");

        builder.Property(i => i.ExpiredAt)
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(i => new { i.HouseholdId, i.Email })
            .IsUnique()
            .HasFilter("status = 'pending' AND invitation_type = 'email' AND email IS NOT NULL")
            .HasDatabaseName("idx_invites_unique");

        builder.HasIndex(i => i.Token)
            .IsUnique()
            .HasFilter("token IS NOT NULL")
            .HasDatabaseName("idx_invites_token");

        builder.HasIndex(i => new { i.HouseholdId, i.Status });
    }
}
