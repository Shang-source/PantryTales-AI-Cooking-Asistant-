using backend.Models;

namespace backend.Interfaces;

/// <summary>
/// Service for ensuring recipes have cover images.
/// </summary>
public interface IRecipeImageService
{
    /// <summary>
    /// Ensure the recipe has a cover image and return the URL if available.
    /// </summary>
    Task<string?> EnsureCoverImageUrlAsync(
        Recipe recipe,
        CancellationToken cancellationToken = default);
}
