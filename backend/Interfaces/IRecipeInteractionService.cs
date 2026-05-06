using backend.Dtos.Interactions;
using backend.Models;

namespace backend.Interfaces;

public interface IRecipeInteractionService
{
    /// <summary>
    /// Log a single interaction event (click, open, save, like, cook, share, etc.)
    /// </summary>
    Task<bool> LogInteractionAsync(
        string clerkUserId,
        Guid recipeId,
        RecipeInteractionEventType eventType,
        string? source = null,
        string? sessionId = null,
        int? dwellSeconds = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch log impression events (for efficiency when loading feeds)
    /// </summary>
    Task<int> LogImpressionsAsync(
        string clerkUserId,
        IEnumerable<Guid> recipeIds,
        string? source = null,
        string? sessionId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get interaction analytics for a recipe (event counts by type)
    /// </summary>
    Task<RecipeInteractionStatsDto?> GetRecipeStatsAsync(
        Guid recipeId,
        int days = 30,
        CancellationToken cancellationToken = default);
}
