using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

[Table("recipe_interactions")]
public class RecipeInteraction
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

    [Required]
    [Column("event_type")]
    public RecipeInteractionEventType EventType { get; set; }

    /// <summary>
    /// Where the interaction originated (e.g., "home_feed", "search_results", "recipe_detail", "similar_recipes")
    /// </summary>
    [Column("source")]
    [MaxLength(64)]
    public string? Source { get; set; }

    /// <summary>
    /// Optional session identifier to group interactions within a user session
    /// </summary>
    [Column("session_id")]
    [MaxLength(64)]
    public string? SessionId { get; set; }

    /// <summary>
    /// For dwell events: how long the user spent on the recipe (in seconds)
    /// </summary>
    [Column("dwell_seconds")]
    public int? DwellSeconds { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    [ForeignKey(nameof(RecipeId))]
    public Recipe Recipe { get; set; } = null!;
}

public enum RecipeInteractionEventType : byte
{
    Impression = 1,
    Click = 2,
    Open = 3,
    Dwell = 4,
    Save = 5,
    Unsave = 6,
    Like = 7,
    Unlike = 8,
    Cook = 9,
    Share = 10
}
