using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

[Table("inventory_item_tags")]
public class InventoryItemTag
{
    [Column("item_id")]
    public Guid ItemId { get; set; }

    [Column("tag_id")]
    public int TagId { get; set; }

    [ForeignKey(nameof(ItemId))]
    public InventoryItem Item { get; set; } = null!;

    [ForeignKey(nameof(TagId))]
    public Tag Tag { get; set; } = null!;
}
