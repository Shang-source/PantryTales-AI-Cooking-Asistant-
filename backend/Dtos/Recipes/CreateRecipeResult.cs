namespace backend.Dtos.Recipes;

public enum CreateRecipeResultStatus
{
    Success,
    UserNotFound,
    HouseholdNotFound,
    InvalidRequest,
    Failed
}

public sealed record CreateRecipeResult(CreateRecipeResultStatus Status, RecipeDetailDto? Recipe);
