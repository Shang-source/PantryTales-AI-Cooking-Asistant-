using System.Text.Json.Serialization;
using backend.Models;

namespace backend.Dtos.Recipes;

public sealed record RecipeAuthorDto(Guid Id, string Nickname, string? AvatarUrl);

public sealed record RecipeDetailDto(
    Guid Id,
    Guid HouseholdId,
    Guid? AuthorId,
    RecipeAuthorDto? Author,
    string Title,
    string Description,
    IReadOnlyList<string> Steps,
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    RecipeVisibility Visibility,
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    RecipeType Type,
    IReadOnlyList<string>? ImageUrls,
    int LikesCount,
    bool LikedByMe,
    int CommentsCount,
    int SavedCount,
    bool SavedByMe,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<RecipeIngredientDto> Ingredients,
    IReadOnlyList<string> Tags,
    decimal? Servings,
    int? TotalTimeMinutes,
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    RecipeDifficulty Difficulty,
    // Nutrition fields - Calories is actual value, others are % daily value
    decimal? Calories = null,
    decimal? Carbohydrates = null,
    decimal? Fat = null,
    decimal? Protein = null,
    decimal? Sugar = null,
    decimal? Sodium = null,
    decimal? SaturatedFat = null);

