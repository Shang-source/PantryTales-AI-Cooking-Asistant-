using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

[Table("household_members")]
public class HouseholdMember
{
    [Column("household_id")]
    public Guid HouseholdId { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Required]
    [Column("role")]
    [MaxLength(32)]
    public string Role { get; set; } = "member";

    [Required]
    [Column("display_name")]
    [MaxLength(128)]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    [Column("email")]
    [MaxLength(128)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Column("joined_at")]
    public DateTime JoinedAt { get; init; } = DateTime.UtcNow;

    [ForeignKey(nameof(HouseholdId))]
    public Household Household { get; set; } = null!;

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;
}
