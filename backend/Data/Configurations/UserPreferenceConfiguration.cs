using backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Data.Configurations;

public class UserPreferenceConfiguration : IEntityTypeConfiguration<UserPreference>
{
    public void Configure(EntityTypeBuilder<UserPreference> builder)
    {
        builder.HasKey(p => new { p.UserId, p.TagId });

        builder.HasOne(p => p.User)
            .WithMany(u => u.Preferences)
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Tag)
            .WithMany(t => t.UserPreferences)
            .HasForeignKey(p => p.TagId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(p => p.Relation)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasColumnType("text");

        builder.Property(p => p.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()");

        builder.HasIndex(p => p.TagId);
    }
}
