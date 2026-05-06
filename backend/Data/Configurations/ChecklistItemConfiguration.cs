using backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Data.Configurations;

public class ChecklistItemConfiguration : IEntityTypeConfiguration<ChecklistItem>
{
    public void Configure(EntityTypeBuilder<ChecklistItem> builder)
    {
        builder.Property(c => c.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.HasOne(c => c.Household)
            .WithMany(h => h.ChecklistItems)
            .HasForeignKey(c => c.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Ingredient)
            .WithMany(i => i.ChecklistItems)
            .HasForeignKey(c => c.IngredientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.FromRecipe)
            .WithMany(r => r.ChecklistItems)
            .HasForeignKey(c => c.FromRecipeId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(c => c.AddedByUser)
            .WithMany(u => u.AddedChecklistItems)
            .HasForeignKey(c => c.AddedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(c => c.Amount)
            .HasPrecision(10, 2)
            .HasDefaultValue(1m);

        builder.Property(c => c.Unit)
            .HasMaxLength(64);

        builder.Property(c => c.Name)
            .HasMaxLength(128);

        builder.Property(c => c.IsChecked)
            .HasDefaultValue(false);

        builder.Property(c => c.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()");

        builder.Property(c => c.UpdatedAt)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()");

        builder.HasIndex(c => new { c.HouseholdId, c.IsChecked });
    }
}
