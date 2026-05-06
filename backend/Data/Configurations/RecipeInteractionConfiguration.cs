using backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Data.Configurations;

public class RecipeInteractionConfiguration : IEntityTypeConfiguration<RecipeInteraction>
{
    public void Configure(EntityTypeBuilder<RecipeInteraction> builder)
    {
        builder.Property(ri => ri.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.HasOne(ri => ri.User)
            .WithMany()
            .HasForeignKey(ri => ri.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ri => ri.Recipe)
            .WithMany()
            .HasForeignKey(ri => ri.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(ri => ri.EventType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasColumnType("text");

        builder.Property(ri => ri.Source)
            .HasMaxLength(64);

        builder.Property(ri => ri.SessionId)
            .HasMaxLength(64);

        builder.Property(ri => ri.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()");

        // Indices for common query patterns
        builder.HasIndex(ri => ri.UserId);
        builder.HasIndex(ri => ri.RecipeId);
        builder.HasIndex(ri => ri.EventType);
        builder.HasIndex(ri => ri.CreatedAt);

        // Composite index for user activity queries
        builder.HasIndex(ri => new { ri.UserId, ri.EventType, ri.CreatedAt });

        // Composite index for recipe analytics queries
        builder.HasIndex(ri => new { ri.RecipeId, ri.EventType, ri.CreatedAt });
    }
}
