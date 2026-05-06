using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

[Table("ingredient_units")]
public class IngredientUnit
{
    [Required]
    [Column("ingredient_id")]
    public Guid IngredientId { get; set; }

    [ForeignKey(nameof(IngredientId))]
    public Ingredient Ingredient { get; set; } = null!;

    [Required]
    [Column("unit_name")]
    public string UnitName { get; set; } = string.Empty;

    [Required]
    [Column("grams_per_unit")]
    public decimal GramsPerUnit { get; set; }
}
