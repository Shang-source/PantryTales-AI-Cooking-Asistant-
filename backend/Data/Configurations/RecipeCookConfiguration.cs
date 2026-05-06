using backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Data.Configurations;

public class RecipeCookConfiguration : IEntityTypeConfiguration<RecipeCook>
{
    public void Configure(EntityTypeBuilder<RecipeCook> builder)
    {
        builder.HasKey(rc => rc.Id);

        builder.HasOne(rc => rc.User)
            .WithMany(u => u.RecipeCooks)
            .HasForeignKey(rc => rc.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(rc => rc.Recipe)
            .WithMany(r => r.Cooks)
            .HasForeignKey(rc => rc.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(rc => rc.CookCount)
            .HasDefaultValue(1);

        builder.Property(rc => rc.FirstCookedAt)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()");

        builder.Property(rc => rc.LastCookedAt)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()");

        // Index for querying user's cooking history
        builder.HasIndex(rc => rc.UserId);

        // Index for querying recipe's cook count
        builder.HasIndex(rc => rc.RecipeId);

        // Unique constraint: one entry per user-recipe pair
        builder.HasIndex(rc => new { rc.UserId, rc.RecipeId })
            .IsUnique();
    }
}
