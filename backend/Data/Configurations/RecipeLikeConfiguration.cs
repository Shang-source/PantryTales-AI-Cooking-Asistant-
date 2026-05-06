using backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Data.Configurations;

public class RecipeLikeConfiguration : IEntityTypeConfiguration<RecipeLike>
{
    public void Configure(EntityTypeBuilder<RecipeLike> builder)
    {
        builder.HasKey(rl => new { rl.UserId, rl.RecipeId });

        builder.HasOne(rl => rl.User)
            .WithMany(u => u.RecipeLikes)
            .HasForeignKey(rl => rl.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(rl => rl.Recipe)
            .WithMany(r => r.Likes)
            .HasForeignKey(rl => rl.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(rl => rl.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()");

        builder.HasIndex(rl => rl.RecipeId);
    }
}
