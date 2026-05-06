using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

[Table("checklists")]
public class ChecklistItem
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    [Required]
    [Column("household_id")]
    public Guid HouseholdId { get; set; }

    [ForeignKey(nameof(HouseholdId))]
    public Household Household { get; set; } = null!;

    [Column("ingredient_id")]
    public Guid? IngredientId { get; set; }

    [ForeignKey(nameof(IngredientId))]
    public Ingredient? Ingredient { get; set; }

    [Required]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Column("amount")]
    public decimal Amount { get; set; } = 1m;

    [Required]
    [Column("unit")]
    public string Unit { get; set; } = string.Empty;

    [Column("category")]
    public string? Category { get; set; }

    [Column("from_recipe_id")]
    public Guid? FromRecipeId { get; set; }

    [ForeignKey(nameof(FromRecipeId))]
    public Recipe? FromRecipe { get; set; }

    [Column("added_by")]
    public Guid? AddedByUserId { get; set; }

    [ForeignKey(nameof(AddedByUserId))]
    public User? AddedByUser { get; set; }

    [Column("is_checked")]
    public bool IsChecked { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
