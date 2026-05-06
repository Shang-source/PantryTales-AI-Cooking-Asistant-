using System.Text.Json.Serialization;
using backend.Models;

namespace backend.Dtos.Recipes;

public sealed record MySavedRecipeCardDto(
    Guid Id,
    string Title,
    string? Description,
    string? CoverImageUrl,
    Guid? AuthorId,
    string AuthorName,
    int SavedCount,
    bool SavedByMe,
    DateTime SavedAt,
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    RecipeType Type);

