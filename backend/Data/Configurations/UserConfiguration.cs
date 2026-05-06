using backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        // 1. Constraints & Indices
        builder.HasIndex(e => e.ClerkUserId).IsUnique();
        builder.HasIndex(e => e.Email).IsUnique();

        // 2. Numeric Precision
        builder.Property(e => e.Height).HasPrecision(4, 1);
        builder.Property(e => e.Weight).HasPrecision(5, 2);

        builder.Property(e => e.Embedding)
            .HasColumnType("vector(768)");

        builder.Property(e => e.EmbeddingStatus)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasColumnType("text")
            .HasDefaultValue(UserEmbeddingStatus.Pending);

        builder.Property(e => e.EmbeddingUpdatedAt)
            .HasColumnType("timestamp with time zone");

        // 3. TimeZones & Defaults
        builder.Property(e => e.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()");

        builder.Property(e => e.UpdatedAt)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()");
    }
}
