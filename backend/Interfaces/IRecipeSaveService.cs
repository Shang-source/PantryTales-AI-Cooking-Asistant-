using backend.Dtos.Recipes;

namespace backend.Interfaces;

public enum SavesCategory
{
    All,
    Recommended,
    Community,
    Generated
}

public interface IRecipeSaveService
{
    Task<RecipeSaveResponseDto?> ToggleSaveAsync(Guid recipeId, string clerkUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MySavedRecipeCardDto>?> GetMySavedRecipesAsync(string clerkUserId, int page, int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MySavedRecipeCardDto>?> GetMySavedRecipesAsync(string clerkUserId, int page, int pageSize,
        SavesCategory category, CancellationToken cancellationToken = default);

    Task<int?> GetMySavesCountAsync(string clerkUserId, CancellationToken cancellationToken = default);

    Task<int?> GetMySavesCountAsync(string clerkUserId, SavesCategory category,
        CancellationToken cancellationToken = default);
}
