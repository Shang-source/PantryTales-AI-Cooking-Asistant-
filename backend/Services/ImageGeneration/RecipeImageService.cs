using System.Globalization;
using backend.Data;
using backend.Interfaces;
using backend.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace backend.Services.ImageGeneration;

/// <summary>
/// Ensures recipes have cover images by generating and storing them when missing.
/// </summary>
public class RecipeImageService : IRecipeImageService
{
    private readonly AppDbContext _dbContext;
    private readonly IImageGenerationProvider _imageGenerationProvider;
    private readonly IImageStorageService _imageStorageService;
    private readonly ILogger<RecipeImageService> _logger;

    public RecipeImageService(
        AppDbContext dbContext,
        IImageGenerationProvider imageGenerationProvider,
        IImageStorageService imageStorageService,
        ILogger<RecipeImageService> logger)
    {
        _dbContext = dbContext;
        _imageGenerationProvider = imageGenerationProvider;
        _imageStorageService = imageStorageService;
        _logger = logger;
    }

    public async Task<string?> EnsureCoverImageUrlAsync(
        Recipe recipe,
        CancellationToken cancellationToken = default)
    {
        if (recipe.ImageUrls is { Count: > 0 })
        {
            return recipe.ImageUrls[0];
        }

        try
        {
            _logger.LogInformation("Generating cover image for recipe {RecipeId}", recipe.Id);
            var prompt = BuildPrompt(recipe);
            if (string.IsNullOrWhiteSpace(prompt))
            {
                return null;
            }

            var generationResult = await _imageGenerationProvider.GenerateImageAsync(
                new ImageGenerationRequest(prompt),
                cancellationToken);

            if (!generationResult.Success ||
                generationResult.ImageData == null ||
                string.IsNullOrWhiteSpace(generationResult.MimeType))
            {
                _logger.LogWarning(
                    "Image generation failed for recipe {RecipeId}: {Reason}",
                    recipe.Id,
                    generationResult.ErrorMessage ?? "Unknown error");
                return null;
            }

            var imageUrl = await UploadImageAsync(recipe, generationResult, cancellationToken);
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                return null;
            }

            if (_dbContext.Entry(recipe).State == EntityState.Detached)
            {
                _dbContext.Recipes.Attach(recipe);
            }

            recipe.ImageUrls = new List<string> { imageUrl };
            recipe.UpdatedAt = DateTime.UtcNow;

            var entry = _dbContext.Entry(recipe);
            entry.Property(r => r.ImageUrls).IsModified = true;
            entry.Property(r => r.UpdatedAt).IsModified = true;

            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Cover image stored for recipe {RecipeId}", recipe.Id);
            return imageUrl;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to ensure cover image for recipe {RecipeId}", recipe.Id);
            return null;
        }
    }

    private async Task<string?> UploadImageAsync(
        Recipe recipe,
        ImageGenerationResult generationResult,
        CancellationToken cancellationToken)
    {
        try
        {
            if (generationResult.ImageData == null ||
                string.IsNullOrWhiteSpace(generationResult.MimeType))
            {
                return null;
            }

            var fileName = $"recipe-{recipe.Id:N}.png";
            var mimeType = generationResult.MimeType;
            using var stream = new MemoryStream(generationResult.ImageData);
            var file = new FormFile(stream, 0, stream.Length, "file", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = mimeType
            };

            return await _imageStorageService.UploadAsync(file, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to upload generated image for recipe {RecipeId}", recipe.Id);
            return null;
        }
    }

    private static string? TruncateAtWordBoundary(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || maxLength <= 0)
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length <= maxLength)
        {
            return trimmed;
        }

        var cutIndex = maxLength;
        for (var i = maxLength - 1; i >= 0; i--)
        {
            if (char.IsWhiteSpace(trimmed[i]))
            {
                cutIndex = i;
                break;
            }
        }

        var truncated = trimmed[..cutIndex].TrimEnd();
        return truncated.Length == 0 ? trimmed[..maxLength] : truncated;
    }

    private static string BuildPrompt(Recipe recipe)
    {
        var title = recipe.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        var description = TruncateAtWordBoundary(recipe.Description, 200);
        var ingredients = recipe.Ingredients?
            .Select(ri => ri.Ingredient?.CanonicalName?.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList()
            ?? [];

        var tags = recipe.Tags?
            .Select(rt => rt.Tag?.DisplayName?.Trim() ?? rt.Tag?.Name?.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList()
            ?? [];

        var promptParts = new List<string>
        {
            $"Photorealistic food photography of {title}."
        };

        if (!string.IsNullOrWhiteSpace(description))
        {
            promptParts.Add($"{description}.");
        }

        if (ingredients.Count > 0)
        {
            promptParts.Add($"Key ingredients: {string.Join(", ", ingredients)}.");
        }

        if (tags.Count > 0)
        {
            promptParts.Add($"Style cues: {string.Join(", ", tags)}.");
        }

        if (recipe.TotalTimeMinutes is > 0)
        {
            promptParts.Add($"Prepared in about {recipe.TotalTimeMinutes} minutes.");
        }

        if (recipe.Servings is > 0)
        {
            var servingsText = recipe.Servings.Value.ToString("0.#", CultureInfo.InvariantCulture);
            promptParts.Add($"Serves {servingsText}.");
        }

        promptParts.Add(
            "Plated on a clean ceramic dish, natural window light, shallow depth of field, 3/4 angle, realistic textures, appetizing, clean background, no text, no watermark, no people, no utensils in foreground, no packaging.");

        return string.Join(" ", promptParts);
    }
}
