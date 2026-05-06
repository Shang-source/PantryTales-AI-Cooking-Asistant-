using backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Data.Configurations;

public class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> builder)
    {
        builder.Property(i => i.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.HasOne(i => i.Household)
            .WithMany(h => h.InventoryItems)
            .HasForeignKey(i => i.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.Ingredient)
            .WithMany(ing => ing.InventoryItems)
            .HasForeignKey(i => i.IngredientId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.Property(i => i.NormalizedName)
            .HasMaxLength(256);

        builder.Property(i => i.NameNormalizationRemovedTokens)
            .HasColumnType("jsonb");

        builder.Property(i => i.ResolveStatus)
            .HasColumnType("smallint")
            .HasDefaultValue(IngredientResolveStatus.Pending);

        builder.Property(i => i.ResolveConfidence)
            .HasPrecision(5, 4);

        builder.Property(i => i.ResolveMethod)
            .HasMaxLength(64);

        builder.Property(i => i.ResolvedAt)
            .HasColumnType("timestamp with time zone");

        builder.Property(i => i.ResolveAttempts)
            .HasDefaultValue(0);

        builder.Property(i => i.LastResolveError)
            .HasMaxLength(2048);

        builder.HasIndex(i => i.NormalizedName);
        builder.HasIndex(i => new { i.ResolveStatus, i.ResolveAttempts });
        builder.HasIndex(i => new { i.HouseholdId, i.Status });

        builder.Property(i => i.Amount)
            .HasPrecision(10, 2)
            .HasDefaultValue(1m);

        builder.Property(i => i.Unit)
            .HasMaxLength(64);

        builder.Property(i => i.StorageMethod)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasColumnType("text");

        builder.Property(i => i.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasColumnType("text");

        builder.Property(i => i.ExpirationDate)
            .HasColumnType("date");

        builder.Property(i => i.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()");

        builder.Property(i => i.UpdatedAt)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()");
    }
}
