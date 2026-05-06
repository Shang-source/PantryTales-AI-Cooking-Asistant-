using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

[Table("recipe_likes")]
public class RecipeLike
{
    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("recipe_id")]
    public Guid RecipeId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    [ForeignKey(nameof(RecipeId))]
    public Recipe Recipe { get; set; } = null!;

    [Column("created_at")]
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
