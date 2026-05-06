using System.ComponentModel.DataAnnotations;

namespace backend.Dtos.Knowledgebase;

public sealed record CreateArticleRequestDto
{
    [Required]
    [StringLength(256)]
    public string Title { get; init; } = string.Empty;

    [StringLength(512)]
    public string? Subtitle { get; init; }

    [StringLength(64)]
    public string? IconName { get; init; }

    [Required]
    [StringLength(100000)]
    public string Content { get; init; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int TagId { get; init; }

    public bool IsPublished { get; init; } = true;
}
