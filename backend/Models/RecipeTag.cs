using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

[Table("recipe_tags")]
public class RecipeTag
{
    [Column("recipe_id")]
    public Guid RecipeId { get; set; }

    [Column("tag_id")]
    public int TagId { get; set; }

    [ForeignKey(nameof(RecipeId))]
    public Recipe Recipe { get; set; } = null!;

    [ForeignKey(nameof(TagId))]
    public Tag Tag { get; set; } = null!;
}
