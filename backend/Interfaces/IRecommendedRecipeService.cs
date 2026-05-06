using backend.Models;

namespace backend.Interfaces;

/// <summary>
/// Service for retrieving personalized recipe recommendations based on user preferences.
/// Unlike SmartRecipeService, this does not use AI generation or inventory data.
/// </summary>
public interface IRecommendedRecipeService
{
    /// <summary>
    /// Get recommended recipes for a user based on their preferences, allergies, and restrictions.
    /// </summary>
    /// <param name="userId">Internal user identifier.</param>
    /// <param name="limit">Maximum number of recipes to return.</param>
    /// <param name="offset">Offset for pagination.</param>
    /// <param name="search">Optional search term to filter by title or tags.</param>
    /// <param name="seed">
    /// Optional seed used to stabilize the randomized ordering across pagination.
    /// Provide a stable value for the duration of a refresh or session (for example, a short random string).
    /// </param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    Task<RecommendedRecipeResult> GetRecommendationsAsync(
        Guid userId,
        int limit = 20,
        int offset = 0,
        string? search = null,
        string? seed = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of recommended recipe retrieval.
/// </summary>
public record RecommendedRecipeResult(
    RecommendedRecipeResultStatus Status,
    IReadOnlyList<RecommendedRecipeDto>? Recipes = null,
    int TotalCount = 0,
    string? ErrorMessage = null);

public enum RecommendedRecipeResultStatus
{
    Success,
    UserNotFound,
    NoRecipesAvailable
}

/// <summary>
/// DTO for recommended recipe.
/// </summary>
public record RecommendedRecipeDto(
    Guid RecipeId,
    string Title,
    string? Description,
    string? CoverImageUrl,
    int? TotalTimeMinutes,
    RecipeDifficulty Difficulty,
    decimal? Servings,
    int PreferenceMatchCount,
    int LikesCount,
    int SavedCount,
    bool SavedByMe,
    IReadOnlyList<string> Tags,
    RecipeType Type,
    Guid? AuthorId,
    string? AuthorNickname,
    string? AuthorAvatarUrl);
