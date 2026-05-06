namespace backend.Dtos.Recipes;

public enum UpdateRecipeResultStatus
{
    Success,
    UserNotFound,
    RecipeNotFound,
    Unauthorized,
    InvalidRequest,
    Failed
}

public sealed record UpdateRecipeResult(
    UpdateRecipeResultStatus Status,
    RecipeDetailDto? Recipe = null,
    string? FailureReason = null);

