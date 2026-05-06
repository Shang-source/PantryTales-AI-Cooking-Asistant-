using backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Data.Configurations;

public class RecipeIngredientTagConfiguration : IEntityTypeConfiguration<RecipeIngredientTag>
{
    public void Configure(EntityTypeBuilder<RecipeIngredientTag> builder)
    {
        builder.HasKey(t => new { t.RecipeIngredientId, t.TagId });

        builder.HasOne(t => t.RecipeIngredient)
            .WithMany(ri => ri.Tags)
            .HasForeignKey(t => t.RecipeIngredientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.Tag)
            .WithMany(tag => tag.RecipeIngredientTags)
            .HasForeignKey(t => t.TagId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.TagId);
    }
}
