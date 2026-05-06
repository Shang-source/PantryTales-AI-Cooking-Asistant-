using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

[Table("household_invitations")]
public class HouseholdInvitation
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    [Required]
    [Column("household_id")]
    public Guid HouseholdId { get; set; }

    [ForeignKey(nameof(HouseholdId))]
    public Household Household { get; set; } = null!;

    [Required]
    [Column("inviter_id")]
    public Guid InviterId { get; set; }

    [ForeignKey(nameof(InviterId))]
    public User Inviter { get; set; } = null!;

    [Column("email")]
    [EmailAddress]
    [MaxLength(256)]
    public string? Email { get; set; }

    [Required]
    [Column("invitation_type")]
    [MaxLength(16)]
    public string InvitationType { get; set; } = "email";

    [Column("token")]
    [MaxLength(32)]
    public string? Token { get; set; }

    [Required]
    [Column("status")]
    [MaxLength(32)]
    public string Status { get; set; } = "pending";

    [Column("expired_at")]
    public DateTime ExpiredAt { get; set; } = DateTime.UtcNow.AddDays(7);

    [Column("created_at")]
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
