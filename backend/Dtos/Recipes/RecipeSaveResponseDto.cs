namespace backend.Dtos.Recipes;

public sealed record RecipeSaveResponseDto(
    Guid RecipeId,
    bool IsSaved,
    int SavesCount
);
