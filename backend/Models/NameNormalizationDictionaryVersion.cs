using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

[Table("name_normalization_dictionary_versions")]
public class NameNormalizationDictionaryVersion
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Id { get; set; } = 1;

    [Required]
    [Column("dictionary_version")]
    public long DictionaryVersion { get; set; } = 1;

    [Required]
    [Column("algorithm_version")]
    public int AlgorithmVersion { get; set; } = 1;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
