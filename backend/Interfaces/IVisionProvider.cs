namespace backend.Interfaces;

/// <summary>
/// Provider-agnostic interface for AI vision services (image recognition).
/// Implementations can use Gemini, AWS Bedrock, or other providers.
/// </summary>
public interface IVisionProvider
{
    /// <summary>
    /// Name of the provider (e.g., "Gemini", "Bedrock").
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Recognize ingredients from an image.
    /// </summary>
    /// <param name="imageData">The raw image bytes.</param>
    /// <param name="mimeType">The MIME type of the image (e.g., "image/jpeg").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Recognition result with detected ingredients.</returns>
    Task<IngredientRecognitionResult> RecognizeIngredientsAsync(
        byte[] imageData,
        string mimeType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Recognize a recipe from an image of a dish.
    /// </summary>
    /// <param name="imageData">The raw image bytes.</param>
    /// <param name="mimeType">The MIME type of the image (e.g., "image/jpeg").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Recognition result with detected recipe.</returns>
    Task<RecipeRecognitionResult> RecognizeRecipeAsync(
        byte[] imageData,
        string mimeType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate recipe content (steps, tags, ingredients) from images and context.
    /// </summary>
    /// <param name="imagesData">List of raw image bytes.</param>
    /// <param name="mimeTypes">List of MIME types for each image.</param>
    /// <param name="title">Recipe title provided by user.</param>
    /// <param name="description">Optional recipe description.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Generated recipe content.</returns>
    Task<GenerateRecipeContentResult> GenerateRecipeContentAsync(
        List<byte[]> imagesData,
        List<string> mimeTypes,
        string title,
        string? description,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of ingredient recognition from an image.
/// </summary>
public record IngredientRecognitionResult(
    bool Success,
    string ImageType,  // "receipt", "ingredients", or "unknown"
    List<RecognizedIngredient> Ingredients,
    string? StoreName = null,
    List<FilteredItem>? FilteredItems = null,
    string? Notes = null,
    string? ErrorMessage = null
);

/// <summary>
/// A single recognized ingredient with metadata.
/// </summary>
public record RecognizedIngredient(
    string Name,
    decimal Quantity,
    string Unit,
    double Confidence,
    string? SuggestedStorageMethod = null,
    int? SuggestedExpirationDays = null,
    string? OriginalReceiptText = null
);

/// <summary>
/// An item that was filtered out during receipt scanning.
/// </summary>
public record FilteredItem(
    string Text,
    string Reason
);

/// <summary>
/// Result of recipe recognition from a dish image.
/// </summary>
public record RecipeRecognitionResult(
    bool Success,
    RecognizedRecipe? Recipe,
    string? ErrorMessage = null
);

/// <summary>
/// A single ingredient in a recognized recipe with structured data.
/// </summary>
public record VisionRecipeIngredient(
    string Name,
    decimal Quantity,
    string Unit,
    string? Category = null
);

/// <summary>
/// A recognized recipe with details.
/// </summary>
public record RecognizedRecipe(
    string Title,
    string Description,
    List<VisionRecipeIngredient> Ingredients,
    List<string> Steps,
    int? PrepTimeMinutes,
    int? CookTimeMinutes,
    int? Servings,
    double Confidence,
    RecipeNutrition? Nutrition = null
);

/// <summary>
/// Nutrition information for a recipe (estimated by AI).
/// </summary>
public record RecipeNutrition(
    decimal? Calories,
    decimal? Carbohydrates,
    decimal? Fat,
    decimal? Protein,
    decimal? Sugar,
    decimal? Sodium,
    decimal? SaturatedFat
);

/// <summary>
/// Result of AI recipe content generation.
/// </summary>
public record GenerateRecipeContentResult(
    bool Success,
    string? Description,
    List<string>? Steps,
    List<string>? Tags,
    List<VisionGeneratedIngredient>? Ingredients,
    double? Confidence,
    string? ErrorMessage = null
);

/// <summary>
/// An AI-generated ingredient with structured data (for vision API).
/// </summary>
public record VisionGeneratedIngredient(
    string Name,
    decimal? Amount,
    string? Unit,
    string? Category
);
