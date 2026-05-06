using backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Data.Configurations;

public class RecipeSaveConfiguration : IEntityTypeConfiguration<RecipeSave>
{
    public void Configure(EntityTypeBuilder<RecipeSave> builder)
    {
        builder.HasKey(rs => new { rs.UserId, rs.RecipeId });

        builder.HasOne(rs => rs.User)
            .WithMany(u => u.RecipeSaves)
            .HasForeignKey(rs => rs.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(rs => rs.Recipe)
            .WithMany(r => r.Saves)
            .HasForeignKey(rs => rs.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(rs => rs.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()");

        builder.HasIndex(rs => rs.RecipeId);
    }
}
