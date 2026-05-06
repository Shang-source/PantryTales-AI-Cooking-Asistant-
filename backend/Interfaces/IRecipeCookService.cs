using backend.Dtos.Recipes;

namespace backend.Interfaces;

public interface IRecipeCookService
{
    /// <summary>
    /// Record that a user completed cooking a recipe
    /// </summary>
    Task<RecipeCookResponseDto?> RecordCookAsync(Guid recipeId, string clerkUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get user's cooking history (sorted by cook count descending)
    /// </summary>
    Task<IReadOnlyList<MyCookedRecipeCardDto>?> GetMyCookedRecipesAsync(
        string clerkUserId,
        int page,
        int pageSize,
        string? searchQuery,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get count of unique recipes the user has cooked
    /// </summary>
    Task<int?> GetMyCooksCountAsync(string clerkUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a specific cooking history entry
    /// </summary>
    Task<bool> DeleteCookEntryAsync(Guid cookId, string clerkUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clear all cooking history for a user
    /// </summary>
    Task<bool> ClearAllCookHistoryAsync(string clerkUserId,
        CancellationToken cancellationToken = default);
}
