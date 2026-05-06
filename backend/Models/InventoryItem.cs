using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

public enum InventoryItemStatus : byte
{
    Active = 1,
    Consumed = 2,
    Discarded = 3
}

public enum IngredientResolveStatus : byte
{
    Pending = 1,
    Resolved = 2,
    NeedsReview = 3,
    Failed = 4
}

public enum InventoryStorageMethod : byte
{
    RoomTemp = 1,
    Refrigerated = 2,
    Frozen = 3,
    Other = 9
}

[Table("inventory_items")]
public class InventoryItem
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    [Required]
    [Column("household_id")]
    public Guid HouseholdId { get; set; }

    [ForeignKey(nameof(HouseholdId))]
    public Household Household { get; set; } = null!;

    [Column("ingredient_id")]
    public Guid? IngredientId { get; set; }

    [ForeignKey(nameof(IngredientId))]
    public Ingredient? Ingredient { get; set; }

    [Required]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("normalized_name")]
    public string? NormalizedName { get; set; }

    [Column("name_normalization_dictionary_version")]
    public long? NameNormalizationDictionaryVersion { get; set; }

    [Column("name_normalization_algorithm_version")]
    public int? NameNormalizationAlgorithmVersion { get; set; }

    [Column("name_normalization_removed_tokens", TypeName = "jsonb")]
    public string? NameNormalizationRemovedTokens { get; set; }

    [Required]
    [Column("ingredient_resolve_status")]
    public IngredientResolveStatus ResolveStatus { get; set; } = IngredientResolveStatus.Pending;

    [Column("ingredient_resolve_confidence")]
    public decimal? ResolveConfidence { get; set; }

    [Column("ingredient_resolve_method")]
    public string? ResolveMethod { get; set; }

    [Column("ingredient_resolved_at")]
    public DateTime? ResolvedAt { get; set; }

    [Required]
    [Column("ingredient_resolve_attempts")]
    public int ResolveAttempts { get; set; } = 0;

    [Column("ingredient_last_resolve_error")]
    public string? LastResolveError { get; set; }

    [Required]
    [Column("amount")]
    public decimal Amount { get; set; } = 1m;

    [Required]
    [Column("unit")]
    public string Unit { get; set; } = string.Empty;

    [Required]
    [Column("storage_method")]
    public InventoryStorageMethod StorageMethod { get; set; } = InventoryStorageMethod.RoomTemp;

    [Column("expiration_date")]
    public DateOnly? ExpirationDate { get; set; }

    [Required]
    [Column("status")]
    public InventoryItemStatus Status { get; set; } = InventoryItemStatus.Active;

    [Column("created_at")]
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<InventoryItemTag> Tags { get; set; } = [];
}
