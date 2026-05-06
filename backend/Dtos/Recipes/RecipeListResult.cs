namespace backend.Dtos.Recipes;

public enum RecipeListResultStatus
{
    Success,
    InvalidScope,
    MissingClerkUserId,
    UserNotFound,
    Failed
}

public sealed record RecipeListResult(RecipeListResultStatus Status, IReadOnlyList<RecipeCardDto>? Recipes,
    string? FailureReason = null);
