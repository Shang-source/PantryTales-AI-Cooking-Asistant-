using backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Data.Configurations;

public class RecipeConfiguration : IEntityTypeConfiguration<Recipe>
{
    public void Configure(EntityTypeBuilder<Recipe> builder)
    {
        builder.Property(r => r.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.HasOne(r => r.Household)
            .WithMany(h => h.Recipes)
            .HasForeignKey(r => r.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.Author)
            .WithMany(u => u.AuthoredRecipes)
            .HasForeignKey(r => r.AuthorId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(r => r.Title)
            .HasMaxLength(256);

        builder.Property(r => r.Type)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasColumnType("text");

        builder.Property(r => r.Visibility)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasColumnType("text");

        builder.Property(r => r.Difficulty)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasColumnType("text");

        builder.Property(r => r.Embedding)
            .HasColumnType("vector(768)");

        builder.Property(r => r.EmbeddingStatus)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasColumnType("text")
            .HasDefaultValue(RecipeEmbeddingStatus.Pending);

        builder.Property(r => r.Steps)
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'[]'::jsonb");

        builder.Property(r => r.ImageUrls)
            .HasColumnType("text[]");

        builder.Property(r => r.Servings)
            .HasPrecision(6, 2);

        builder.Property(r => r.Calories)
            .HasPrecision(10, 2);

        builder.Property(r => r.Fat)
            .HasPrecision(6, 2);

        builder.Property(r => r.Sugar)
            .HasPrecision(6, 2);

        builder.Property(r => r.Sodium)
            .HasPrecision(6, 2);

        builder.Property(r => r.Protein)
            .HasPrecision(6, 2);

        builder.Property(r => r.SaturatedFat)
            .HasPrecision(6, 2);

        builder.Property(r => r.Carbohydrates)
            .HasPrecision(6, 2);

        builder.Property(r => r.LikesCount).HasDefaultValue(0);
        builder.Property(r => r.CommentsCount).HasDefaultValue(0);
        builder.Property(r => r.SavedCount).HasDefaultValue(0);
        builder.Property(r => r.IsFeatured).HasDefaultValue(false);

        builder.Property(r => r.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()");

        builder.Property(r => r.UpdatedAt)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()");

        builder.Property(r => r.EmbeddingUpdatedAt)
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(r => new { r.HouseholdId, r.Visibility });
        builder.HasIndex(r => new { r.Type, r.Visibility });
        builder.HasIndex(r => r.AuthorId);
        builder.HasIndex(r => new { r.IsFeatured, r.Visibility, r.Type });
    }
}
