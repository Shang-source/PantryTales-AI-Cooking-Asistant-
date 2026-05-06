using backend.Models;

namespace backend.Interfaces;

public interface IRecipeCookRepository
{
    /// <summary>
    /// Get existing cook entry for a user-recipe pair
    /// </summary>
    Task<RecipeCook?> GetByUserAndRecipeAsync(Guid userId, Guid recipeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Add a new cook entry
    /// </summary>
    Task AddAsync(RecipeCook recipeCook, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a cook entry by ID
    /// </summary>
    Task<RecipeCook?> GetByIdAsync(Guid cookId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all cook entries for a user (sorted by cook count desc, then last cooked desc)
    /// </summary>
    Task<IReadOnlyList<RecipeCook>> GetUserCookingHistoryAsync(
        Guid userId,
        int page,
        int pageSize,
        string? searchQuery,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the count of unique recipes a user has cooked
    /// </summary>
    Task<int> GetUniqueRecipeCountAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a specific cook entry
    /// </summary>
    Task DeleteAsync(RecipeCook recipeCook, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete all cook entries for a user
    /// </summary>
    Task DeleteAllForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
