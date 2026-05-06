using backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Data.Configurations;

public class IngredientAliasConfiguration : IEntityTypeConfiguration<IngredientAlias>
{
    public void Configure(EntityTypeBuilder<IngredientAlias> builder)
    {
        builder.Property(a => a.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.HasOne(a => a.Ingredient)
            .WithMany(i => i.Aliases)
            .HasForeignKey(a => a.IngredientId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.Property(a => a.AliasName)
            .HasMaxLength(256);

        builder.Property(a => a.NormalizedName)
            .HasMaxLength(256);

        builder.Property(a => a.NameNormalizationRemovedTokens)
            .HasColumnType("jsonb");

        builder.Property(a => a.Source)
            .HasMaxLength(64);

        builder.Property(a => a.Confidence)
            .HasPrecision(3, 2);

        builder.Property(a => a.Status)
            .HasColumnType("smallint")
            .HasDefaultValue(AliasResolveStatus.Pending);

        builder.Property(a => a.ResolveMethod)
            .HasMaxLength(64);

        builder.Property(a => a.ResolvedAt)
            .HasColumnType("timestamp with time zone");

        builder.Property(a => a.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()");

        builder.Property(a => a.UpdatedAt)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()");

        builder.HasIndex(a => new { a.Source, a.NormalizedName })
            .IsUnique();

        builder.HasIndex(a => a.NormalizedName);

        builder.HasIndex(a => a.IngredientId);
    }
}
