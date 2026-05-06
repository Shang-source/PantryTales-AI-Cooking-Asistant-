using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using backend.Models;

namespace backend.Dtos.Recipes;

public class CreateRecipeRequestDto
{
    [Required]
    [MaxLength(256)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Optional description for the recipe. Can be AI-generated or user-provided.
    /// </summary>
    public string? Description { get; set; }

    [Required]
    [MinLength(1)]
    public List<string> Steps { get; set; } = [];

    [Required]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RecipeVisibility Visibility { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RecipeType? Type { get; set; }

    public List<string>? ImageUrls { get; set; }

    public List<string>? Tags { get; set; }

    public decimal? Servings { get; set; }

    public int? TotalTimeMinutes { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RecipeDifficulty? Difficulty { get; set; }

    /// <summary>
    /// Optional list of ingredients for the recipe.
    /// </summary>
    public List<CreateRecipeIngredientDto>? Ingredients { get; set; }

    /// <summary>
    /// Optional nutrition data calculated from ingredients.
    /// </summary>
    public RecipeNutritionDto? Nutrition { get; set; }
}

/// <summary>
/// DTO for creating a recipe ingredient.
/// </summary>
public class CreateRecipeIngredientDto
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public decimal? Amount { get; set; }

    public string? Unit { get; set; }

    public bool IsOptional { get; set; } = false;

    public string? Category { get; set; }
}

/// <summary>
/// DTO for recipe nutrition information.
/// </summary>
public class RecipeNutritionDto
{
    public decimal? Calories { get; set; }
    public decimal? Carbohydrates { get; set; }
    public decimal? Fat { get; set; }
    public decimal? Protein { get; set; }
    public decimal? Sugar { get; set; }
    public decimal? Sodium { get; set; }
    public decimal? SaturatedFat { get; set; }
}
