using backend.Models;

namespace backend.Interfaces;

public interface IRecipeRepository
{
    Task<Recipe?> GetByIdAsync(Guid recipeId, CancellationToken cancellationToken = default);

    Task<Recipe?> GetDetailedByIdAsync(Guid recipeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get featured community recipes for the homepage carousel.
    /// Returns public User-type recipes where IsFeatured is true.
    /// </summary>
    Task<List<Recipe>> GetFeaturedRecipesAsync(int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Toggle the featured status of a recipe.
    /// </summary>
    Task ToggleFeaturedAsync(Guid recipeId, CancellationToken cancellationToken = default);
}
