using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Pgvector;

namespace backend.Models;

public enum IngredientEmbeddingStatus : byte
{
    Pending = 1,
    Ready = 2,
    Error = 3
}

[Table("ingredients")]
public class Ingredient
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    [Required]
    [Column("canonical_name")]
    public string CanonicalName { get; set; } = string.Empty;

    [Column("default_unit")]
    public string? DefaultUnit { get; set; }

    [Column("kcal_per_100g")]
    public decimal? KcalPer100g { get; set; }

    [Column("protein_g_per_100g")]
    public decimal? ProteinPer100g { get; set; }

    [Column("fat_g_per_100g")]
    public decimal? FatPer100g { get; set; }

    [Column("saturated_fat_g_per_100g")]
    public decimal? SaturatedFatPer100g { get; set; }

    [Column("unsaturated_fat_g_per_100g")]
    public decimal? UnsaturatedFatPer100g { get; set; }

    [Column("trans_fat_g_per_100g")]
    public decimal? TransFatPer100g { get; set; }

    [Column("carb_g_per_100g")]
    public decimal? CarbPer100g { get; set; }

    [Column("sugar_g_per_100g")]
    public decimal? SugarPer100g { get; set; }

    [Column("fiber_g_per_100g")]
    public decimal? FiberPer100g { get; set; }

    [Column("cholesterol_mg_per_100g")]
    public decimal? CholesterolPer100g { get; set; }

    [Column("sodium_mg_per_100g")]
    public decimal? SodiumPer100g { get; set; }

    [Column("default_days_to_expire")]
    public int? DefaultDaysToExpire { get; set; }

    [Column("image_url")]
    public string? ImageUrl { get; set; }

    [Column("embedding", TypeName = "vector(768)")]
    public Vector? Embedding { get; set; }

    [Required]
    [Column("embedding_status")]
    public IngredientEmbeddingStatus EmbeddingStatus { get; set; } = IngredientEmbeddingStatus.Pending;

    [Column("embedding_updated_at")]
    public DateTime? EmbeddingUpdatedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<IngredientAlias> Aliases { get; set; } = [];
    public ICollection<IngredientUnit> Units { get; set; } = [];
    public ICollection<InventoryItem> InventoryItems { get; set; } = [];
    public ICollection<RecipeIngredient> RecipeIngredients { get; set; } = [];
    public ICollection<ChecklistItem> ChecklistItems { get; set; } = [];
    public ICollection<IngredientTag> Tags { get; set; } = [];
}
