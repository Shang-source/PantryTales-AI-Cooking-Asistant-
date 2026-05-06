using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

[Table("recipe_ingredient_tags")]
public class RecipeIngredientTag
{
    [Column("ingredient_id")]
    public Guid RecipeIngredientId { get; set; }

    [Column("tag_id")]
    public int TagId { get; set; }

    [ForeignKey(nameof(RecipeIngredientId))]
    public RecipeIngredient RecipeIngredient { get; set; } = null!;

    [ForeignKey(nameof(TagId))]
    public Tag Tag { get; set; } = null!;
}
