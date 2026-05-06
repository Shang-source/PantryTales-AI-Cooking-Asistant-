using System.ComponentModel.DataAnnotations;
using backend.Models;

namespace backend.Dtos.Admin;

public class AdminRecipeQueryParameters
{
    private const int MaxPageSize = 100;

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    private int _pageSize = 20;

    [Range(1, MaxPageSize)]
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = Math.Clamp(value, 1, MaxPageSize);
    }

    [MaxLength(256)]
    public string? Search { get; set; }

    public RecipeType? Type { get; set; }

    public RecipeVisibility? Visibility { get; set; }
}

public record AdminRecipeListItemDto(
    Guid Id,
    string Title,
    string? Description,
    RecipeType Type,
    RecipeVisibility Visibility,
    Guid? AuthorId,
    string? AuthorNickname,
    int LikesCount,
    int CommentsCount,
    int SavedCount,
    RecipeDifficulty Difficulty,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record AdminRecipeListResponseDto(
    int Total,
    int Page,
    int PageSize,
    IReadOnlyList<AdminRecipeListItemDto> Items);

public class AdminUpdateRecipeRequestDto
{
    [Required]
    [MaxLength(256)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    public RecipeType Type { get; set; }

    [Required]
    public RecipeVisibility Visibility { get; set; }

    public RecipeDifficulty Difficulty { get; set; } = RecipeDifficulty.None;
}
