using System.Text.Json.Serialization;
using backend.Models;

namespace backend.Dtos.Recipes;

/// <summary>
/// DTO for featured community recipes displayed in the homepage carousel.
/// </summary>
public sealed record FeaturedRecipeDto(
    Guid Id,
    string Title,
    string? Description,
    string? CoverImageUrl,
    int? TotalTimeMinutes,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] RecipeDifficulty Difficulty,
    decimal? Servings,
    int LikesCount,
    int SavedCount,
    Guid? AuthorId,
    string? AuthorNickname,
    string? AuthorAvatarUrl,
    IReadOnlyList<string> Tags);
