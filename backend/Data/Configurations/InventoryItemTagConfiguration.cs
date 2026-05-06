using backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Data.Configurations;

public class InventoryItemTagConfiguration : IEntityTypeConfiguration<InventoryItemTag>
{
    public void Configure(EntityTypeBuilder<InventoryItemTag> builder)
    {
        builder.HasKey(t => new { t.ItemId, t.TagId });

        builder.HasOne(t => t.Item)
            .WithMany(i => i.Tags)
            .HasForeignKey(t => t.ItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.Tag)
            .WithMany(tag => tag.InventoryItemTags)
            .HasForeignKey(t => t.TagId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.TagId);
    }
}
