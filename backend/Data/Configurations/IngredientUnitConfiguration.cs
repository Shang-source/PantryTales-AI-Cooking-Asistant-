using backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Data.Configurations;

public class IngredientUnitConfiguration : IEntityTypeConfiguration<IngredientUnit>
{
    public void Configure(EntityTypeBuilder<IngredientUnit> builder)
    {
        builder.HasKey(u => new { u.IngredientId, u.UnitName });

        builder.HasOne(u => u.Ingredient)
            .WithMany(i => i.Units)
            .HasForeignKey(u => u.IngredientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(u => u.UnitName)
            .HasMaxLength(64);

        builder.Property(u => u.GramsPerUnit)
            .HasPrecision(10, 3);
    }
}
