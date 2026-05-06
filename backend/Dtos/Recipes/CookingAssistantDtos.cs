namespace backend.Dtos.Recipes;

public record CookingStepDto(
    int Order,
    string Instruction,
    int? SuggestedSeconds);

public record CookingSessionDto(
    Guid RecipeId,
    string Title,
    int TotalSteps,
    IReadOnlyList<CookingStepDto> Steps);
