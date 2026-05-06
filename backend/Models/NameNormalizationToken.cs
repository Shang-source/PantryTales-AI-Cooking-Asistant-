using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

public enum NameNormalizationTokenCategory : byte
{
    Brand = 1,
    Unit = 2,
    Packaging = 3,
    Promo = 4,
    Noise = 99
}

[Table("name_normalization_tokens")]
public class NameNormalizationToken
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Required]
    [Column("token")]
    public string Token { get; set; } = string.Empty;

    [Required]
    [Column("category")]
    public NameNormalizationTokenCategory Category { get; set; } = NameNormalizationTokenCategory.Noise;

    [Required]
    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Required]
    [Column("is_regex")]
    public bool IsRegex { get; set; } = false;

    [Column("language")]
    public string? Language { get; set; }

    [Column("source")]
    public string? Source { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
