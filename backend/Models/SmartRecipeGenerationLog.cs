using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

/// <summary>
/// Tracks smart recipe generation events for rate limiting.
/// Each record represents one generation attempt by a user.
/// </summary>
[Table("smart_recipe_generation_logs")]
public class SmartRecipeGenerationLog
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    [Required]
    [Column("user_id")]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    [Column("generated_at")]
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
}
