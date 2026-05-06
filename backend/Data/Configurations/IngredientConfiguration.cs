using backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Data.Configurations;

public class IngredientConfiguration : IEntityTypeConfiguration<Ingredient>
{
    public void Configure(EntityTypeBuilder<Ingredient> builder)
    {
        builder.Property(i => i.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(i => i.CanonicalName)
            .HasMaxLength(256);

        builder.Property(i => i.DefaultUnit)
            .HasMaxLength(64)
            .IsRequired(false);

        builder.Property(i => i.DefaultDaysToExpire)
            .IsRequired(false);

        builder.Property(i => i.KcalPer100g).HasPrecision(10, 2);
        builder.Property(i => i.ProteinPer100g).HasPrecision(10, 2);
        builder.Property(i => i.FatPer100g).HasPrecision(10, 2);
        builder.Property(i => i.SaturatedFatPer100g).HasPrecision(10, 2);
        builder.Property(i => i.UnsaturatedFatPer100g).HasPrecision(10, 2);
        builder.Property(i => i.TransFatPer100g).HasPrecision(10, 2);
        builder.Property(i => i.CarbPer100g).HasPrecision(10, 2);
        builder.Property(i => i.SugarPer100g).HasPrecision(10, 2);
        builder.Property(i => i.FiberPer100g).HasPrecision(10, 2);
        builder.Property(i => i.CholesterolPer100g).HasPrecision(10, 2);
        builder.Property(i => i.SodiumPer100g).HasPrecision(10, 2);

        builder.Property(i => i.ImageUrl)
            .HasMaxLength(512);

        builder.Property(i => i.Embedding)
            .HasColumnType("vector(768)");

        builder.Property(i => i.EmbeddingStatus)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasColumnType("text")
            .HasDefaultValue(IngredientEmbeddingStatus.Pending);

        builder.Property(i => i.EmbeddingUpdatedAt)
            .HasColumnType("timestamp with time zone");

        builder.Property(i => i.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()");

        builder.Property(i => i.UpdatedAt)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()");
    }
}
