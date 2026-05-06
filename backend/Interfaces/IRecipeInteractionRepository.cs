using backend.Models;

namespace backend.Interfaces;

public interface IRecipeInteractionRepository
{
    Task AddAsync(RecipeInteraction interaction, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<RecipeInteraction> interactions, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get interactions for a user, optionally filtered by event type and time range.
    /// </summary>
    Task<List<RecipeInteraction>> GetByUserIdAsync(
        Guid userId,
        RecipeInteractionEventType? eventType = null,
        DateTime? since = null,
        int? limit = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get interactions for a recipe, optionally filtered by event type and time range.
    /// </summary>
    Task<List<RecipeInteraction>> GetByRecipeIdAsync(
        Guid recipeId,
        RecipeInteractionEventType? eventType = null,
        DateTime? since = null,
        int? limit = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Count interactions by event type for a recipe (useful for trending/popularity).
    /// </summary>
    Task<Dictionary<RecipeInteractionEventType, int>> GetEventCountsForRecipeAsync(
        Guid recipeId,
        DateTime? since = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get recipe IDs the user has interacted with (for user embedding calculation).
    /// </summary>
    Task<List<(Guid RecipeId, RecipeInteractionEventType EventType, DateTime CreatedAt)>> GetUserInteractionSummaryAsync(
        Guid userId,
        DateTime? since = null,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
