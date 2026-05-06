namespace backend.Dtos.Recipes;

public sealed record RecipeIngredientDto(
    Guid RecipeIngredientId,
    Guid IngredientId,
    string Name,
    decimal? Amount,
    string? Unit,
    bool IsOptional,
    string? Category);
