using backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Data.Configurations;

public class IngredientTagConfiguration : IEntityTypeConfiguration<IngredientTag>
{
    public void Configure(EntityTypeBuilder<IngredientTag> builder)
    {
        builder.HasKey(t => new { t.IngredientId, t.TagId });

        builder.HasOne(t => t.Ingredient)
            .WithMany(i => i.Tags)
            .HasForeignKey(t => t.IngredientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.Tag)
            .WithMany(tag => tag.IngredientTags)
            .HasForeignKey(t => t.TagId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.TagId);
    }
}
