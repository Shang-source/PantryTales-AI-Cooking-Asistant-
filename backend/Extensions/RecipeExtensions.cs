using System.Text.Json;
using backend.Dtos.Recipes;
using backend.Models;

namespace backend.Extensions;

public static class RecipeExtensions
{
    private const string RecipeTagType = "recipe";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static RecipeDetailDto ToDetailDto(this Recipe recipe)
        => recipe.ToDetailDto(false, false);

    public static RecipeDetailDto ToDetailDto(this Recipe recipe, bool likedByMe)
        => recipe.ToDetailDto(likedByMe, false);

    public static RecipeDetailDto ToDetailDto(this Recipe recipe, bool likedByMe, bool savedByMe)
    {
        var steps = DeserializeSteps(recipe.Steps);

        var tags = recipe.Tags
            .Where(rt => rt.Tag is { IsActive: true } &&
                         string.Equals(rt.Tag.Type, RecipeTagType, StringComparison.Ordinal))
            .Select(rt => rt.Tag!.DisplayName ?? string.Empty)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name)
            .ToList();

        var ingredients = recipe.Ingredients
            .Select(ingredient =>
            {
                var originalUnit = ingredient.Unit?.Trim();
                var unit = originalUnit;
                if (string.IsNullOrWhiteSpace(unit) || IsPlaceholderUnit(unit))
                {
                    unit = ingredient.Ingredient?.DefaultUnit?.Trim();
                }

                if (string.IsNullOrWhiteSpace(unit) && ingredient.Ingredient?.Units is { Count: > 0 })
                {
                    unit = ingredient.Ingredient.Units
                        .OrderBy(u => u.GramsPerUnit)
                        .Select(u => u.UnitName)
                        .FirstOrDefault();
                }
                if (string.IsNullOrWhiteSpace(unit))
                {
                    unit = originalUnit;
                }

                // First try recipe-ingredient-specific tags, then fall back to ingredient's own category tags
                // Filter for ingredient_type tags specifically
                const string ingredientTypeTag = "ingredient_type";
                var category = ingredient.Tags
                    .Select(tag => tag.Tag)
                    .FirstOrDefault(tag => tag.IsActive && tag.Type == ingredientTypeTag)
                    ?? ingredient.Ingredient?.Tags
                        .Select(tag => tag.Tag)
                        .FirstOrDefault(tag => tag.IsActive && tag.Type == ingredientTypeTag);
                var categoryLabel = category?.DisplayName ?? category?.Name;

                return new RecipeIngredientDto(
                    ingredient.Id,
                    ingredient.IngredientId,
                    ingredient.Ingredient?.CanonicalName ?? string.Empty,
                    ingredient.Amount,
                    string.IsNullOrWhiteSpace(unit) ? null : unit,
                    ingredient.IsOptional,
                    string.IsNullOrWhiteSpace(categoryLabel) ? null : categoryLabel);
            })
            .ToList();

        RecipeAuthorDto? author = null;
        if (recipe.AuthorId.HasValue && recipe.Author is not null)
        {
            author = new RecipeAuthorDto(recipe.AuthorId.Value, recipe.Author.Nickname, recipe.Author.AvatarUrl);
        }

        return new RecipeDetailDto(
            recipe.Id,
            recipe.HouseholdId,
            recipe.AuthorId,
            author,
            recipe.Title.ToTitleCase(),
            recipe.Description ?? string.Empty,
            steps,
            recipe.Visibility,
            recipe.Type,
            recipe.ImageUrls,
            recipe.LikesCount,
            likedByMe,
            recipe.CommentsCount,
            recipe.SavedCount,
            savedByMe,
            recipe.CreatedAt,
            recipe.UpdatedAt,
            ingredients,
            tags,
            recipe.Servings,
            recipe.TotalTimeMinutes,
            recipe.Difficulty,
            recipe.Calories,
            recipe.Carbohydrates,
            recipe.Fat,
            recipe.Protein,
            recipe.Sugar,
            recipe.Sodium,
            recipe.SaturatedFat);
    }

    private static List<string> DeserializeSteps(string stepsJson)
    {
        if (string.IsNullOrWhiteSpace(stepsJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(stepsJson, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static bool IsPlaceholderUnit(string unit)
        => string.Equals(unit, "unit", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(unit, "units", StringComparison.OrdinalIgnoreCase);
}
