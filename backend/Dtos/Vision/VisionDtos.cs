namespace backend.Dtos.Vision;

/// <summary>
/// Response DTO for ingredient recognition.
/// </summary>
public record IngredientRecognitionResponseDto(
    bool Success,
    string ImageType,  // "receipt", "ingredients", or "unknown"
    List<RecognizedIngredientDto> Ingredients,
    string? StoreName = null,
    List<FilteredItemDto>? FilteredItems = null,
    string? Notes = null,
    string? ErrorMessage = null
);

/// <summary>
/// A single recognized ingredient.
/// </summary>
public record RecognizedIngredientDto(
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
public record FilteredItemDto(
    string Text,
    string Reason
);

/// <summary>
/// Response DTO for recipe recognition.
/// </summary>
public record RecipeRecognitionResponseDto(
    bool Success,
    RecognizedRecipeDto? Recipe,
    string? ErrorMessage = null
);

/// <summary>
/// A single ingredient in a recipe with structured data.
/// </summary>
public record RecipeIngredientDto(
    string Name,
    decimal Quantity,
    string Unit,
    string? Category = null
);

/// <summary>
/// A recognized recipe with details.
/// </summary>
public record RecognizedRecipeDto(
    string Title,
    string Description,
    List<RecipeIngredientDto> Ingredients,
    List<string> Steps,
    int? PrepTimeMinutes,
    int? CookTimeMinutes,
    int? Servings,
    double Confidence,
    RecipeNutritionDto? Nutrition = null
);

/// <summary>
/// Nutrition information for a recipe (estimated by AI).
/// </summary>
public record RecipeNutritionDto(
    decimal? Calories,
    decimal? Carbohydrates,
    decimal? Fat,
    decimal? Protein,
    decimal? Sugar,
    decimal? Sodium,
    decimal? SaturatedFat
);

/// <summary>
/// Request DTO for generating recipe content from images.
/// </summary>
public record GenerateRecipeContentRequestDto(
    List<string> ImageUrls,
    string Title,
    string? Description = null
);

/// <summary>
/// Response DTO for AI-generated recipe content.
/// </summary>
public record GenerateRecipeContentResponseDto(
    bool Success,
    string? Description,
    List<string>? Steps,
    List<string>? Tags,
    List<GeneratedIngredientDto>? Ingredients,
    double? Confidence,
    string? ErrorMessage = null
);

/// <summary>
/// An AI-generated ingredient with structured data.
/// </summary>
public record GeneratedIngredientDto(
    string Name,
    decimal? Amount,
    string? Unit,
    string? Category
);

