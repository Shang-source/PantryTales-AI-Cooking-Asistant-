using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Pgvector;

namespace backend.Models;

public enum RecipeType : byte
{
    User = 1,
    System = 2,
    Model = 3
}

public enum RecipeVisibility : byte
{
    Private = 1,
    Public = 2
}

public enum RecipeDifficulty : byte
{
    None = 0,
    Easy = 1,
    Medium = 2,
    Hard = 3
}

public enum RecipeEmbeddingStatus : byte
{
    Pending = 1,
    Ready = 2,
    Error = 3
}

[Table("recipes")]
public class Recipe
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
    [Column("type")]
    public RecipeType Type { get; set; } = RecipeType.User;

    [Column("author_id")]
    public Guid? AuthorId { get; set; }

    [ForeignKey(nameof(AuthorId))]
    public User? Author { get; set; }

    [Required]
    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Column("servings")]
    public decimal? Servings { get; set; }

    [Column("total_time_minutes")]
    public int? TotalTimeMinutes { get; set; }

    // Food.com dataset nutrition fields:
    // calories (#), then % daily value (PDV) for the rest.
    [Column("calories")]
    public decimal? Calories { get; set; }

    [Column("fat")]
    public decimal? Fat { get; set; }

    [Column("sugar")]
    public decimal? Sugar { get; set; }

    [Column("sodium")]
    public decimal? Sodium { get; set; }

    [Column("protein")]
    public decimal? Protein { get; set; }

    [Column("saturated_fat")]
    public decimal? SaturatedFat { get; set; }

    [Column("carbohydrates")]
    public decimal? Carbohydrates { get; set; }

    [Column("difficulty")]
    public RecipeDifficulty Difficulty { get; set; } = RecipeDifficulty.None;

    [Column("image_urls")]
    public List<string>? ImageUrls { get; set; }

    [Required]
    [Column("steps", TypeName = "jsonb")]
    public string Steps { get; set; } = "[]";

    [Required]
    [Column("visibility")]
    public RecipeVisibility Visibility { get; set; } = RecipeVisibility.Private;

    [Required]
    [Column("likes_count")]
    public int LikesCount { get; set; }

    [Required]
    [Column("comments_count")]
    public int CommentsCount { get; set; }

    [Required]
    [Column("saved_count")]
    public int SavedCount { get; set; }

    [Column("is_featured")]
    public bool IsFeatured { get; set; }

    [Column("embedding", TypeName = "vector(768)")]
    public Vector? Embedding { get; set; }

    [Required]
    [Column("embedding_status")]
    public RecipeEmbeddingStatus EmbeddingStatus { get; set; } = RecipeEmbeddingStatus.Pending;

    [Column("embedding_updated_at")]
    public DateTime? EmbeddingUpdatedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<RecipeIngredient> Ingredients { get; set; } = [];
    public ICollection<RecipeTag> Tags { get; set; } = [];
    public ICollection<RecipeLike> Likes { get; set; } = [];
    public ICollection<RecipeComment> Comments { get; set; } = [];
    public ICollection<RecipeSave> Saves { get; set; } = [];
    public ICollection<RecipeCook> Cooks { get; set; } = [];
    public ICollection<ChecklistItem> ChecklistItems { get; set; } = [];
}
