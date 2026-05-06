namespace backend.Dtos.Recipes;

/// <summary>
/// Request for calculating nutrition from a list of ingredients.
/// </summary>
public record CalculateNutritionRequestDto(
    List<NutritionIngredientDto> Ingredients,
    decimal Servings = 1
);

/// <summary>
/// An ingredient with quantity for nutrition calculation.
/// </summary>
public record NutritionIngredientDto(
    string Name,
    decimal Quantity,
    string Unit
);

/// <summary>
/// Response containing calculated nutrition information.
/// </summary>
public record NutritionResponseDto(
    bool Success,
    NutritionDataDto? Nutrition,
    List<string> Warnings,
    string? ErrorMessage = null
);

/// <summary>
/// Nutrition data calculated from ingredients.
/// </summary>
public record NutritionDataDto(
    decimal Calories,
    decimal Carbohydrates,
    decimal Fat,
    decimal Protein,
    decimal Sugar,
    decimal Sodium,
    decimal SaturatedFat,
    decimal UnsaturatedFat,
    decimal TransFat,
    decimal Fiber,
    decimal Cholesterol,
    decimal Servings
);
