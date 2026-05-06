using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

[Table("ingredient_tags")]
public class IngredientTag
{
    [Column("ingredient_id")]
    public Guid IngredientId { get; set; }

    [Column("tag_id")]
    public int TagId { get; set; }

    [ForeignKey(nameof(IngredientId))]
    public Ingredient Ingredient { get; set; } = null!;

    [ForeignKey(nameof(TagId))]
    public Tag Tag { get; set; } = null!;
}
