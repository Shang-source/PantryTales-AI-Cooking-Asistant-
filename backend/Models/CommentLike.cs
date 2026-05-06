using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace backend.Models;

[Table("comment_likes")]
[PrimaryKey(nameof(UserId), nameof(CommentId))]
public class CommentLike
{
    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("comment_id")]
    public Guid CommentId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    [ForeignKey(nameof(CommentId))]
    public RecipeComment Comment { get; set; } = null!;

    [Column("created_at")]
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

