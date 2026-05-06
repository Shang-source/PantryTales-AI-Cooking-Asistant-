using backend.Dtos.SmartRecipes;
using backend.Models;

namespace backend.Interfaces;

/// <summary>
/// Service for generating and retrieving AI-powered smart recipe suggestions.
/// </summary>
public interface ISmartRecipeService
{
    /// <summary>
    /// Get or generate smart recipes for a user's household.
    /// Generates new recipes if:
    /// - No recipes exist for today's date
    /// - Inventory has changed since last generation
    /// </summary>
    Task<SmartRecipeResult> GetOrGenerateAsync(Guid userId, bool allowStale = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Force regeneration of smart recipes (ignores daily limit).
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="servings">Optional number of servings. Defaults to household size if not specified.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<SmartRecipeResult> ForceRegenerateAsync(Guid userId, int? servings = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidate cached smart recipes for a household.
    /// Called when inventory changes.
    /// </summary>
    Task InvalidateForHouseholdAsync(Guid householdId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stream smart recipes as they are generated via SSE.
    /// Yields individual recipes via IAsyncEnumerable for streaming.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="servings">Optional number of servings. Defaults to household size if not specified.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    IAsyncEnumerable<SmartRecipeSseEvent> StreamGenerateAsync(
        Guid userId,
        int? servings = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of smart recipe generation or retrieval.
/// </summary>
public record SmartRecipeResult(
    SmartRecipeResultStatus Status,
    IReadOnlyList<SmartRecipeDto>? Recipes = null,
    string? ErrorMessage = null);

public enum SmartRecipeResultStatus
{
    Success,
    NoInventory,
    NoHousehold,
    GenerationFailed,
    UserNotFound
}

/// <summary>
/// DTO for smart recipe with missing ingredients info.
/// </summary>
public record SmartRecipeDto(
    Guid Id,
    Guid RecipeId,
    string Title,
    string? Description,
    string? CoverImageUrl,
    int? TotalTimeMinutes,
    RecipeDifficulty Difficulty,
    decimal? Servings,
    int MissingIngredientsCount,
    IReadOnlyList<string> MissingIngredients,
    decimal? MatchScore,
    DateOnly GeneratedDate,
    DateTime CreatedAt,
    IReadOnlyList<SmartRecipeIngredientDto> Ingredients);

/// <summary>
/// DTO for smart recipe ingredient.
/// </summary>
public record SmartRecipeIngredientDto(
    string Name,
    decimal? Amount,
    string? Unit,
    bool IsOptional,
    string? Category = null);
