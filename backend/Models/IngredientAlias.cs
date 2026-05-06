using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

public enum AliasResolveStatus : byte
{
    Pending = 1,
    Resolved = 2,
    NeedsReview = 3,
    Rejected = 4
}

[Table("ingredient_aliases")]
public class IngredientAlias
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    [Column("ingredient_id")]
    public Guid? IngredientId { get; set; }

    [ForeignKey(nameof(IngredientId))]
    public Ingredient? Ingredient { get; set; }

    [Required]
    [Column("alias_name")]
    public string AliasName { get; set; } = string.Empty;

    [Column("normalized_name")]
    public string? NormalizedName { get; set; }

    [Column("name_normalization_dictionary_version")]
    public long? NameNormalizationDictionaryVersion { get; set; }

    [Column("name_normalization_algorithm_version")]
    public int? NameNormalizationAlgorithmVersion { get; set; }

    [Column("name_normalization_removed_tokens", TypeName = "jsonb")]
    public string? NameNormalizationRemovedTokens { get; set; }

    [Column("source")]
    public string? Source { get; set; }

    [Column("confidence")]
    public decimal? Confidence { get; set; }

    [Required]
    [Column("status")]
    public AliasResolveStatus Status { get; set; } = AliasResolveStatus.Pending;

    [Column("resolve_method")]
    public string? ResolveMethod { get; set; }

    [Column("resolved_at")]
    public DateTime? ResolvedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
