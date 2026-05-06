using backend.Dtos.Recipes;
using backend.Interfaces;

namespace backend.Services;

/// <summary>
/// Service for calculating nutrition from ingredients using a built-in nutrition database.
/// </summary>
public class NutritionService : INutritionService
{
    private readonly ILogger<NutritionService> _logger;

    // Basic nutrition data per 100g for common ingredients
    // Format: (calories, carbs, fat, protein, sugar, sodium_mg, saturatedFat, fiber)
    private static readonly Dictionary<string, (decimal cal, decimal carb, decimal fat, decimal protein, decimal sugar, decimal sodium, decimal satFat, decimal fiber)> NutritionDatabase = new(StringComparer.OrdinalIgnoreCase)
    {
        // Proteins
        ["chicken breast"] = (165, 0, 3.6m, 31, 0, 74, 1, 0),
        ["chicken"] = (239, 0, 14, 27, 0, 82, 4, 0),
        ["beef"] = (250, 0, 15, 26, 0, 72, 6, 0),
        ["pork"] = (242, 0, 14, 27, 0, 62, 5, 0),
        ["salmon"] = (208, 0, 13, 20, 0, 59, 3, 0),
        ["shrimp"] = (99, 0.2m, 0.3m, 24, 0, 111, 0.1m, 0),
        ["egg"] = (155, 1.1m, 11, 13, 1.1m, 124, 3.3m, 0),
        ["eggs"] = (155, 1.1m, 11, 13, 1.1m, 124, 3.3m, 0),
        ["tofu"] = (76, 1.9m, 4.8m, 8, 0.6m, 7, 0.7m, 0.3m),

        // Vegetables
        ["onion"] = (40, 9.3m, 0.1m, 1.1m, 4.2m, 4, 0, 1.7m),
        ["garlic"] = (149, 33, 0.5m, 6.4m, 1, 17, 0.1m, 2.1m),
        ["tomato"] = (18, 3.9m, 0.2m, 0.9m, 2.6m, 5, 0, 1.2m),
        ["carrot"] = (41, 10, 0.2m, 0.9m, 4.7m, 69, 0, 2.8m),
        ["broccoli"] = (34, 7, 0.4m, 2.8m, 1.7m, 33, 0.1m, 2.6m),
        ["spinach"] = (23, 3.6m, 0.4m, 2.9m, 0.4m, 79, 0.1m, 2.2m),
        ["potato"] = (77, 17, 0.1m, 2, 0.8m, 6, 0, 2.2m),
        ["bell pepper"] = (31, 6, 0.3m, 1, 4.2m, 4, 0.1m, 2.1m),
        ["pepper"] = (31, 6, 0.3m, 1, 4.2m, 4, 0.1m, 2.1m),
        ["mushroom"] = (22, 3.3m, 0.3m, 3.1m, 2, 5, 0, 1),
        ["ginger"] = (80, 18, 0.8m, 1.8m, 1.7m, 13, 0.2m, 2),
        ["cabbage"] = (25, 5.8m, 0.1m, 1.3m, 3.2m, 18, 0, 2.5m),
        ["celery"] = (16, 3, 0.2m, 0.7m, 1.3m, 80, 0, 1.6m),

        // Grains & Starches
        ["rice"] = (130, 28, 0.3m, 2.7m, 0, 1, 0.1m, 0.4m),
        ["pasta"] = (131, 25, 1.1m, 5, 0.6m, 1, 0.2m, 1.8m),
        ["bread"] = (265, 49, 3.2m, 9, 5, 491, 0.7m, 2.7m),
        ["flour"] = (364, 76, 1, 10, 0.3m, 2, 0.2m, 2.7m),
        ["noodles"] = (138, 25, 2.1m, 4.5m, 0.6m, 5, 0.3m, 1.2m),

        // Dairy
        ["milk"] = (42, 5, 1, 3.4m, 5, 44, 0.6m, 0),
        ["cheese"] = (402, 1.3m, 33, 25, 0.5m, 621, 21, 0),
        ["butter"] = (717, 0.1m, 81, 0.9m, 0.1m, 11, 51, 0),
        ["cream"] = (340, 2.8m, 36, 2.1m, 2.9m, 34, 23, 0),
        ["yogurt"] = (59, 3.6m, 3.3m, 10, 3.2m, 36, 2.1m, 0),

        // Fats & Oils
        ["olive oil"] = (884, 0, 100, 0, 0, 2, 14, 0),
        ["oil"] = (884, 0, 100, 0, 0, 0, 15, 0),
        ["vegetable oil"] = (884, 0, 100, 0, 0, 0, 15, 0),

        // Condiments & Sauces
        ["soy sauce"] = (53, 4.9m, 0, 8.1m, 0.4m, 5493, 0, 0.8m),
        ["vinegar"] = (21, 0.9m, 0, 0, 0, 2, 0, 0),
        ["rice vinegar"] = (18, 0, 0, 0, 0, 2, 0, 0),
        ["ketchup"] = (112, 26, 0.1m, 1.7m, 22, 907, 0, 0.3m),
        ["mayonnaise"] = (680, 0.6m, 75, 1, 0.6m, 635, 12, 0),
        ["mustard"] = (66, 5.3m, 4, 4.4m, 3, 1135, 0.2m, 3.3m),

        // Nuts & Seeds
        ["peanuts"] = (567, 16, 49, 26, 4, 18, 7, 8.5m),
        ["peanut"] = (567, 16, 49, 26, 4, 18, 7, 8.5m),
        ["almonds"] = (579, 22, 50, 21, 4.4m, 1, 3.8m, 12.5m),
        ["walnuts"] = (654, 14, 65, 15, 2.6m, 2, 6.1m, 6.7m),
        ["sesame seeds"] = (573, 23, 50, 18, 0.3m, 11, 7, 11.8m),

        // Spices & Seasonings
        ["salt"] = (0, 0, 0, 0, 0, 38758, 0, 0),
        ["sugar"] = (387, 100, 0, 0, 100, 1, 0, 0),
        ["cornstarch"] = (381, 91, 0.1m, 0.3m, 0, 9, 0, 0.9m),
        ["chili pepper"] = (40, 9, 0.4m, 1.9m, 5.3m, 9, 0, 1.5m),
        ["dried chili peppers"] = (282, 50, 15, 12, 41, 1640, 2.5m, 28),
        ["pepper flakes"] = (282, 50, 15, 12, 41, 27, 2.5m, 28),

        // Fruits
        ["apple"] = (52, 14, 0.2m, 0.3m, 10, 1, 0, 2.4m),
        ["banana"] = (89, 23, 0.3m, 1.1m, 12, 1, 0.1m, 2.6m),
        ["lemon"] = (29, 9.3m, 0.3m, 1.1m, 2.5m, 2, 0, 2.8m),
        ["orange"] = (47, 12, 0.1m, 0.9m, 9.4m, 0, 0, 2.4m),
    };

    // Unit conversion to grams
    private static readonly Dictionary<string, decimal> UnitToGrams = new(StringComparer.OrdinalIgnoreCase)
    {
        ["g"] = 1,
        ["gram"] = 1,
        ["grams"] = 1,
        ["kg"] = 1000,
        ["kilogram"] = 1000,
        ["oz"] = 28.35m,
        ["ounce"] = 28.35m,
        ["ounces"] = 28.35m,
        ["lb"] = 453.6m,
        ["pound"] = 453.6m,
        ["pounds"] = 453.6m,
        ["cup"] = 240,
        ["cups"] = 240,
        ["tbsp"] = 15,
        ["tablespoon"] = 15,
        ["tablespoons"] = 15,
        ["tsp"] = 5,
        ["teaspoon"] = 5,
        ["teaspoons"] = 5,
        ["ml"] = 1,
        ["milliliter"] = 1,
        ["l"] = 1000,
        ["liter"] = 1000,
        ["clove"] = 3,
        ["cloves"] = 3,
        ["piece"] = 100,
        ["pieces"] = 100,
        ["pcs"] = 100,
        ["pc"] = 100,
        ["slice"] = 30,
        ["slices"] = 30,
        ["inch"] = 25,
        ["unit"] = 100,
    };

    public NutritionService(ILogger<NutritionService> logger)
    {
        _logger = logger;
    }

    public async Task<NutritionResponseDto> CalculateNutritionAsync(
        CalculateNutritionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request.Ingredients.Count == 0)
        {
            return new NutritionResponseDto(
                false,
                null,
                [],
                "No ingredients provided.");
        }

        var warnings = new List<string>();
        decimal totalCalories = 0;
        decimal totalCarbs = 0;
        decimal totalFat = 0;
        decimal totalProtein = 0;
        decimal totalSugar = 0;
        decimal totalSodium = 0;
        decimal totalSaturatedFat = 0;
        decimal totalFiber = 0;

        foreach (var ingredient in request.Ingredients)
        {
            var grams = ConvertToGrams(ingredient.Quantity, ingredient.Unit);
            var nutritionPer100g = FindNutritionData(ingredient.Name);

            if (nutritionPer100g == null)
            {
                warnings.Add($"Could not find nutrition data for: {ingredient.Name}");
                // Use average vegetable values as fallback
                nutritionPer100g = (30, 5, 0.3m, 1.5m, 2, 20, 0.1m, 1.5m);
            }

            var multiplier = grams / 100m;
            totalCalories += nutritionPer100g.Value.cal * multiplier;
            totalCarbs += nutritionPer100g.Value.carb * multiplier;
            totalFat += nutritionPer100g.Value.fat * multiplier;
            totalProtein += nutritionPer100g.Value.protein * multiplier;
            totalSugar += nutritionPer100g.Value.sugar * multiplier;
            totalSodium += nutritionPer100g.Value.sodium * multiplier;
            totalSaturatedFat += nutritionPer100g.Value.satFat * multiplier;
            totalFiber += nutritionPer100g.Value.fiber * multiplier;
        }

        var servings = request.Servings > 0 ? request.Servings : 1;

        // Calculate unsaturated fat as total fat minus saturated fat
        var unsaturatedFat = Math.Max(0, totalFat - totalSaturatedFat);

        var nutrition = new NutritionDataDto(
            Calories: Math.Round(totalCalories / servings, 1),
            Carbohydrates: Math.Round(totalCarbs / servings, 1),
            Fat: Math.Round(totalFat / servings, 1),
            Protein: Math.Round(totalProtein / servings, 1),
            Sugar: Math.Round(totalSugar / servings, 1),
            Sodium: Math.Round(totalSodium / servings, 1),
            SaturatedFat: Math.Round(totalSaturatedFat / servings, 1),
            UnsaturatedFat: Math.Round(unsaturatedFat / servings, 1),
            TransFat: 0, // Most whole foods don't have trans fat
            Fiber: Math.Round(totalFiber / servings, 1),
            Cholesterol: 0, // Would need more detailed database
            Servings: servings
        );

        _logger.LogInformation(
            "Nutrition calculated for {Count} ingredients: {Calories} cal, {Protein}g protein, {Warnings} warnings",
            request.Ingredients.Count, nutrition.Calories, nutrition.Protein, warnings.Count);

        return new NutritionResponseDto(
            true,
            nutrition,
            warnings);
    }

    private decimal ConvertToGrams(decimal quantity, string unit)
    {
        var normalizedUnit = unit.Trim().ToLowerInvariant();

        if (UnitToGrams.TryGetValue(normalizedUnit, out var gramsPerUnit))
        {
            return quantity * gramsPerUnit;
        }

        // Default assumption: 1 unit = 100g
        _logger.LogDebug("Unknown unit '{Unit}', assuming 100g per unit", unit);
        return quantity * 100;
    }

    private (decimal cal, decimal carb, decimal fat, decimal protein, decimal sugar, decimal sodium, decimal satFat, decimal fiber)? FindNutritionData(string ingredientName)
    {
        var normalized = ingredientName.Trim().ToLowerInvariant();

        // Direct match
        if (NutritionDatabase.TryGetValue(normalized, out var data))
        {
            return data;
        }

        // Partial match - check if any key is contained in the ingredient name or vice versa
        foreach (var (key, value) in NutritionDatabase)
        {
            if (normalized.Contains(key) || key.Contains(normalized))
            {
                return value;
            }
        }

        // Word-level match - filter to words with at least 3 characters
        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 3);
        foreach (var word in words)
        {
            if (NutritionDatabase.TryGetValue(word, out var wordData))
            {
                return wordData;
            }
        }

        return null;
    }
}
