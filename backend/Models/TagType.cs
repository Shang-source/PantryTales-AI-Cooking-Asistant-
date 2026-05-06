using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

[Table("tag_types")]
public class TagType
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [Column("name")]
    [MaxLength(64)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Column("display_name")]
    [MaxLength(128)]
    public string DisplayName { get; set; } = string.Empty;

    [Column("description")]
    [MaxLength(512)]
    public string? Description { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

}
