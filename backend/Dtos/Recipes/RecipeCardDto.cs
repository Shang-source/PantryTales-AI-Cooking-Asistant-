using System.Text.Json.Serialization;
using backend.Models;

namespace backend.Dtos.Recipes;

public sealed record RecipeCardDto(
    Guid Id,
    Guid? AuthorId,
    string AuthorNickname,
    string? AuthorAvatarUrl,
    string Title,
    string Description,
    string? CoverImageUrl,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] RecipeVisibility Visibility,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] RecipeType Type,
    int LikesCount,
    bool LikedByMe,
    int CommentsCount,
    int SavedCount,
    bool SavedByMe,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<string> Tags);
