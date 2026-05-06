using System.Security.Claims;

namespace backend.Interfaces;

/// <summary>
/// Service for AI-powered vision recognition (ingredients and recipes from images).
/// </summary>
public interface IVisionService
{
    /// <summary>
    /// Recognize ingredients from an uploaded image.
    /// </summary>
    /// <param name="imageStream">The image stream.</param>
    /// <param name="fileName">Original file name.</param>
    /// <param name="mimeType">MIME type of the image.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Recognition result with detected ingredients.</returns>
    Task<IngredientRecognitionResult> RecognizeIngredientsAsync(
        Stream imageStream,
        string fileName,
        string mimeType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Recognize a recipe from an uploaded dish image.
    /// </summary>
    /// <param name="imageStream">The image stream.</param>
    /// <param name="fileName">Original file name.</param>
    /// <param name="mimeType">MIME type of the image.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Recognition result with detected recipe.</returns>
    Task<RecipeRecognitionResult> RecognizeRecipeAsync(
        Stream imageStream,
        string fileName,
        string mimeType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate recipe content (steps, tags, ingredients) from image URLs and context.
    /// </summary>
    /// <param name="imageUrls">List of image URLs (already uploaded).</param>
    /// <param name="title">Recipe title provided by user.</param>
    /// <param name="description">Optional recipe description.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Generated recipe content.</returns>
    Task<GenerateRecipeContentResult> GenerateRecipeContentAsync(
        List<string> imageUrls,
        string title,
        string? description,
        CancellationToken cancellationToken = default);
}
