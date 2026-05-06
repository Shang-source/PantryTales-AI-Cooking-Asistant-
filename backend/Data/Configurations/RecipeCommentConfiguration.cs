using backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Data.Configurations;

public class RecipeCommentConfiguration : IEntityTypeConfiguration<RecipeComment>
{
    public void Configure(EntityTypeBuilder<RecipeComment> builder)
    {
        builder.Property(rc => rc.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.HasOne(rc => rc.User)
            .WithMany(u => u.RecipeComments)
            .HasForeignKey(rc => rc.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(rc => rc.Recipe)
            .WithMany(r => r.Comments)
            .HasForeignKey(rc => rc.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(rc => rc.Content)
            .HasMaxLength(4096);

        builder.Property(rc => rc.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()");

        builder.HasIndex(rc => rc.RecipeId);
    }
}
