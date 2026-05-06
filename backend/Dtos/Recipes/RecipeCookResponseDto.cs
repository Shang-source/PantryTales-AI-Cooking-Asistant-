namespace backend.Dtos.Recipes;

/// <summary>
/// Response when a user completes cooking a recipe
/// </summary>
public sealed record RecipeCookResponseDto(
    Guid RecipeId,
    Guid CookId,
    int CookCount,
    DateTime CookedAt);
