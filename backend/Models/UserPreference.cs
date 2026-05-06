using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

public enum UserPreferenceRelation : byte
{
    Like = 1,
    Dislike = 2,
    Allergy = 3,
    Restriction = 4,
    Goal = 5,
    Other = 99
}

[Table("user_preferences")]
public class UserPreference
{
    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("tag_id")]
    public int TagId { get; set; }

    [Column("relation")]
    public UserPreferenceRelation Relation { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    [ForeignKey(nameof(TagId))]
    public Tag Tag { get; set; } = null!;
}
