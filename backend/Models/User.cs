using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Pgvector;

namespace backend.Models;

[Table("users")]
public class User
{
    // UUID v7
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    // TEXT, Required
    [Required]
    [MaxLength(64)]
    [Column("clerk_user_id")]
    public string ClerkUserId { get; set; } = string.Empty;

    // TEXT, Required, Default: Random (Handled in Logic)
    [Required]
    [MaxLength(64)]
    [Column("nickname")]
    public string Nickname { get; set; } = string.Empty;

    // TEXT, Required
    [Required]
    [EmailAddress]
    [MaxLength(128)]
    [Column("email")]
    public string Email { get; set; } = string.Empty;

    // TEXT, Not Required
    [MaxLength(512)]
    [Column("avatar_url")]
    public string? AvatarUrl { get; set; }

    // INT, Not Required (Nullable)
    [Column("age")]
    public int? Age { get; set; }

    // SMALLINT, Not Required -> Mapped to Enum
    [Column("gender")]
    public UserGender? Gender { get; set; }

    // NUMERIC(4, 1), Not Required -> decimal?
    // Example: 180.5
    [Column("height")]
    public decimal? Height { get; set; }

    // NUMERIC(5, 2), Not Required
    // Example: 60.50
    [Column("weight")]
    public decimal? Weight { get; set; }

    public ICollection<Household> OwnedHouseholds { get; set; } = [];
    public ICollection<HouseholdMember> HouseholdMemberships { get; set; } = [];
    public ICollection<HouseholdInvitation> SentInvitations { get; set; } = [];
    public ICollection<UserPreference> Preferences { get; set; } = [];
    public ICollection<Recipe> AuthoredRecipes { get; set; } = [];
    public ICollection<RecipeLike> RecipeLikes { get; set; } = [];
    public ICollection<RecipeComment> RecipeComments { get; set; } = [];
    public ICollection<CommentLike> CommentLikes { get; set; } = [];
    public ICollection<RecipeSave> RecipeSaves { get; set; } = [];
    public ICollection<RecipeCook> RecipeCooks { get; set; } = [];
    public ICollection<ChecklistItem> AddedChecklistItems { get; set; } = [];

    [Column("embedding", TypeName = "vector(768)")]
    public Vector? Embedding { get; set; }

    [Required]
    [Column("embedding_status")]
    public UserEmbeddingStatus EmbeddingStatus { get; set; } = UserEmbeddingStatus.Pending;

    [Column("embedding_updated_at")]
    public DateTime? EmbeddingUpdatedAt { get; set; }

    // TIMESTAMPTZ, Required
    [Column("created_at")]
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    // TIMESTAMPTZ, Required
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;


    // User role for authorization
    [Required]
    [Column("role")]
    public UserRole Role { get; set; } = UserRole.User;
}

public enum UserGender : byte // Mapped to SMALLINT (byte fits in smallint)
{
    Unknown = 0,
    Male = 1,
    Female = 2,
    NotApplicable = 9
}

public enum UserEmbeddingStatus : byte
{
    Pending = 1,
    Ready = 2,
    Error = 3
}

public enum UserRole : byte
{
    User = 0,       // Default - regular user
    Admin = 1,      // Full admin access
    Moderator = 2   // Limited admin access (future)
}
