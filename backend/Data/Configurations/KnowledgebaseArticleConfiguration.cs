using backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Data.Configurations;

public class KnowledgebaseArticleConfiguration : IEntityTypeConfiguration<KnowledgebaseArticle>
{
    public void Configure(EntityTypeBuilder<KnowledgebaseArticle> builder)
    {
        builder.Property(a => a.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.HasOne(a => a.Tag)
            .WithMany(t => t.KnowledgebaseArticles)
            .HasForeignKey(a => a.TagId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(a => a.Title)
            .HasMaxLength(256);

        builder.Property(a => a.Subtitle)
            .HasMaxLength(512);

        builder.Property(a => a.IconName)
            .HasMaxLength(64);

        builder.Property(a => a.Content)
            .HasColumnType("text");

        builder.Property(a => a.IsPublished)
            .HasDefaultValue(false);

        builder.Property(a => a.IsFeatured)
            .HasDefaultValue(false);

        builder.Property(a => a.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()");

        builder.Property(a => a.UpdatedAt)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()");

        builder.HasIndex(a => new { a.TagId, a.IsPublished });
        builder.HasIndex(a => new { a.IsFeatured, a.IsPublished });
    }
}
