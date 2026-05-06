using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

[Table("tags")]
public class Tag
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Column("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    [Column("type")]
    public string Type { get; set; } = string.Empty;

    [Column("icon")]
    public string? Icon { get; set; }

    [Column("color")]
    public string? Color { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<UserPreference> UserPreferences { get; set; } = [];
    public ICollection<InventoryItemTag> InventoryItemTags { get; set; } = [];
    public ICollection<RecipeIngredientTag> RecipeIngredientTags { get; set; } = [];
    public ICollection<RecipeTag> RecipeTags { get; set; } = [];
    public ICollection<KnowledgebaseArticle> KnowledgebaseArticles { get; set; } = [];
    public ICollection<IngredientTag> IngredientTags { get; set; } = [];
}
