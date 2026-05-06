using backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Data.Configurations;

public class NameNormalizationTokenConfiguration : IEntityTypeConfiguration<NameNormalizationToken>
{
    public void Configure(EntityTypeBuilder<NameNormalizationToken> builder)
    {
        builder.Property(t => t.Token)
            .HasMaxLength(256);

        builder.Property(t => t.Category)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasColumnType("text");

        builder.Property(t => t.Language)
            .HasMaxLength(16);

        builder.Property(t => t.Source)
            .HasMaxLength(64);

        builder.Property(t => t.IsActive)
            .HasDefaultValue(true);

        builder.Property(t => t.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()");

        builder.Property(t => t.UpdatedAt)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()");

        builder.HasIndex(t => t.IsActive);
        builder.HasIndex(t => t.Category);
        builder.HasIndex(t => new { t.Token, t.Category })
            .IsUnique();
    }
}
