using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

/// <summary>
/// Represents an AI-generated smart recipe suggestion for a user.
/// Generated on-demand when the user clicks "Smart Recipes" (once per day or on inventory change).
/// Each user has their own smart recipes, even though inventory is shared per-household.
/// </summary>
[Table("smart_recipes")]
public class SmartRecipe
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    [Required]
    [Column("user_id")]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    [Required]
    [Column("recipe_id")]
    public Guid RecipeId { get; set; }

    [ForeignKey(nameof(RecipeId))]
    public Recipe Recipe { get; set; } = null!;

    /// <summary>
    /// Date-based key for daily generation. New day = new generation.
    /// </summary>
    [Required]
    [Column("generated_date")]
    public DateOnly GeneratedDate { get; set; }

    /// <summary>
    /// Number of missing ingredients from user's inventory.
    /// Used for sorting: 0 = can make now, higher = more items to buy.
    /// </summary>
    [Required]
    [Column("missing_ingredients_count")]
    public int MissingIngredientsCount { get; set; }

    /// <summary>
    /// List of ingredient names that are missing from inventory.
    /// </summary>
    [Column("missing_ingredients", TypeName = "jsonb")]
    public string? MissingIngredients { get; set; }

    /// <summary>
    /// AI confidence/match score for this suggestion.
    /// </summary>
    [Column("match_score")]
    public decimal? MatchScore { get; set; }

    /// <summary>
    /// Snapshot of inventory items at generation time.
    /// Used to detect if regeneration is needed on inventory change.
    /// </summary>
    [Column("inventory_snapshot_hash")]
    public string? InventorySnapshotHash { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
