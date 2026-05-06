namespace backend.Dtos.Recipes;

public enum DeleteRecipeResultStatus
{
    Success,
    UserNotFound,
    RecipeNotFound,
    Unauthorized,
    Failed
}

public sealed record DeleteRecipeResult(DeleteRecipeResultStatus Status, string? FailureReason = null);
