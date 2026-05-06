using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

[Table("recipe_ingredients")]
public class RecipeIngredient
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    [Required]
    [Column("recipe_id")]
    public Guid RecipeId { get; set; }

    [ForeignKey(nameof(RecipeId))]
    public Recipe Recipe { get; set; } = null!;

    [Required]
    [Column("ingredient_id")]
    public Guid IngredientId { get; set; }

    [ForeignKey(nameof(IngredientId))]
    public Ingredient Ingredient { get; set; } = null!;

    [Required]
    [Column("amount")]
    public decimal Amount { get; set; }

    [Required]
    [Column("unit")]
    public string Unit { get; set; } = string.Empty;

    [Required]
    [Column("is_optional")]
    public bool IsOptional { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public ICollection<RecipeIngredientTag> Tags { get; set; } = [];
}
