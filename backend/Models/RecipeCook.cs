using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

/// <summary>
/// Records a user's cooking history for a recipe.
/// Each user-recipe pair has exactly one row with a cook count.
/// </summary>
[Table("recipe_cooks")]
public class RecipeCook
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    [Required]
    [Column("user_id")]
    public Guid UserId { get; set; }

    [Required]
    [Column("recipe_id")]
    public Guid RecipeId { get; set; }

    /// <summary>
    /// Number of times the user has cooked this recipe
    /// </summary>
    [Required]
    [Column("cook_count")]
    public int CookCount { get; set; } = 1;

    /// <summary>
    /// When the user first cooked this recipe
    /// </summary>
    [Required]
    [Column("first_cooked_at")]
    public DateTime FirstCookedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the user last cooked this recipe
    /// </summary>
    [Required]
    [Column("last_cooked_at")]
    public DateTime LastCookedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    [ForeignKey(nameof(RecipeId))]
    public Recipe Recipe { get; set; } = null!;
}
