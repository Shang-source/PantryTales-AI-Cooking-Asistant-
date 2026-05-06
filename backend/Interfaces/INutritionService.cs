using backend.Dtos.Recipes;

namespace backend.Interfaces;

/// <summary>
/// Service for calculating nutrition information from ingredients.
/// </summary>
public interface INutritionService
{
    /// <summary>
    /// Calculate nutrition based on ingredients list.
    /// </summary>
    /// <param name="request">The ingredients and servings to calculate nutrition for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Nutrition calculation result.</returns>
    Task<NutritionResponseDto> CalculateNutritionAsync(
        CalculateNutritionRequestDto request,
        CancellationToken cancellationToken = default);
}
