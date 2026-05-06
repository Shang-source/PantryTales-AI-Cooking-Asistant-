namespace backend.Dtos.Recipes;

public enum RecipeDetailResultStatus
{
    Success,
    RecipeNotFound,
    MissingClerkUserId,
    UserNotFound,
    Unauthorized,
    Failed
}

public sealed record RecipeDetailResult(
    RecipeDetailResultStatus Status,
    RecipeDetailDto? Recipe = null,
    string? FailureReason = null);
