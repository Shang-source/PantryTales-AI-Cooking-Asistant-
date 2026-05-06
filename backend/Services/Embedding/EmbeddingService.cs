using System.Text;
using System.Text.Json;
using backend.Data;
using backend.Interfaces;
using backend.Models;
using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace backend.Services.Embedding;

/// <summary>
/// Service for generating embeddings for recipes, ingredients, and users.
/// Builds text representations from entity fields and calls the embedding provider.
/// </summary>
public class EmbeddingService(
    AppDbContext context,
    IEmbeddingProvider provider,
    ILogger<EmbeddingService> logger)
    : IEmbeddingService
{
    #region Recipe Embedding

    public async Task<int> ProcessRecipeBatchAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var recipes = await context.Recipes
            .Include(r => r.Ingredients)
            .ThenInclude(ri => ri.Ingredient)
            .Include(r => r.Tags)
            .ThenInclude(rt => rt.Tag)
            .Where(r => r.EmbeddingStatus == RecipeEmbeddingStatus.Pending)
            .OrderBy(r => r.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (recipes.Count == 0)
            return 0;

        var texts = recipes.Select(BuildRecipeText).ToList();

        try
        {
            var embeddings = await provider.GenerateBatchEmbeddingsAsync(texts, cancellationToken);

            for (var i = 0; i < recipes.Count && i < embeddings.Count; i++)
            {
                recipes[i].Embedding = new Vector(embeddings[i]);
                recipes[i].EmbeddingStatus = RecipeEmbeddingStatus.Ready;
                recipes[i].EmbeddingUpdatedAt = DateTime.UtcNow;
                recipes[i].UpdatedAt = DateTime.UtcNow;
            }

            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Generated embeddings for {Count} recipes.", embeddings.Count);
            return embeddings.Count;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Batch embedding failed for recipes, trying individual processing.");

            // Fallback: try processing each recipe individually
            var successCount = 0;
            foreach (var recipe in recipes)
            {
                try
                {
                    var embedding = await provider.GenerateEmbeddingAsync(BuildRecipeText(recipe), cancellationToken);
                    if (embedding.Length > 0)
                    {
                        recipe.Embedding = new Vector(embedding);
                        recipe.EmbeddingStatus = RecipeEmbeddingStatus.Ready;
                        recipe.EmbeddingUpdatedAt = DateTime.UtcNow;
                        recipe.UpdatedAt = DateTime.UtcNow;
                        successCount++;
                    }
                    else
                    {
                        recipe.EmbeddingStatus = RecipeEmbeddingStatus.Error;
                    }
                }
                catch
                {
                    recipe.EmbeddingStatus = RecipeEmbeddingStatus.Error;
                }
            }

            await context.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Individually processed {Success}/{Total} recipes.", successCount, recipes.Count);
            return successCount;
        }
    }

    private string BuildRecipeText(Recipe recipe)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"Recipe: {recipe.Title}");

        if (!string.IsNullOrWhiteSpace(recipe.Description))
            sb.AppendLine($"Description: {recipe.Description}");

        if (recipe.Difficulty != RecipeDifficulty.None)
            sb.AppendLine($"Difficulty: {recipe.Difficulty}");

        if (recipe.TotalTimeMinutes.HasValue)
            sb.AppendLine($"Cooking time: {recipe.TotalTimeMinutes} minutes");

        // Ingredients
        var ingredientNames = recipe.Ingredients
            .Select(ri => ri.Ingredient.CanonicalName)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();

        if (ingredientNames.Count > 0)
            sb.AppendLine($"Ingredients: {string.Join(", ", ingredientNames)}");

        // Tags
        var tagNames = recipe.Tags
            .Select(rt => rt.Tag.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();

        if (tagNames.Count > 0)
            sb.AppendLine($"Tags: {string.Join(", ", tagNames)}");

        // Steps (parse JSON and join)
        if (string.IsNullOrWhiteSpace(recipe.Steps) || recipe.Steps == "[]") return sb.ToString().Trim();
        try
        {
            var steps = JsonSerializer.Deserialize<List<string>>(recipe.Steps);
            if (steps is { Count: > 0 })
                sb.AppendLine($"Steps: {string.Join("; ", steps)}");
        }
        catch (JsonException ex)
        {
            logger.LogWarning("Recipe {RecipeId} has malformed Steps JSON: {Error}", recipe.Id, ex.Message);
        }

        return sb.ToString().Trim();
    }

    #endregion

    #region Ingredient Embedding

    public async Task<int> ProcessIngredientBatchAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var ingredients = await context.Ingredients
            .Include(i => i.Aliases)
            .Where(i => i.EmbeddingStatus == IngredientEmbeddingStatus.Pending)
            .OrderBy(i => i.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (ingredients.Count == 0)
            return 0;

        var texts = ingredients.Select(BuildIngredientText).ToList();

        try
        {
            var embeddings = await provider.GenerateBatchEmbeddingsAsync(texts, cancellationToken);

            for (var i = 0; i < ingredients.Count && i < embeddings.Count; i++)
            {
                ingredients[i].Embedding = new Vector(embeddings[i]);
                ingredients[i].EmbeddingStatus = IngredientEmbeddingStatus.Ready;
                ingredients[i].EmbeddingUpdatedAt = DateTime.UtcNow;
                ingredients[i].UpdatedAt = DateTime.UtcNow;
            }

            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Generated embeddings for {Count} ingredients.", embeddings.Count);
            return embeddings.Count;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Batch embedding failed for ingredients, trying individual processing.");

            var successCount = 0;
            foreach (var ingredient in ingredients)
            {
                try
                {
                    var embedding = await provider.GenerateEmbeddingAsync(BuildIngredientText(ingredient), cancellationToken);
                    if (embedding.Length > 0)
                    {
                        ingredient.Embedding = new Vector(embedding);
                        ingredient.EmbeddingStatus = IngredientEmbeddingStatus.Ready;
                        ingredient.EmbeddingUpdatedAt = DateTime.UtcNow;
                        ingredient.UpdatedAt = DateTime.UtcNow;
                        successCount++;
                    }
                    else
                    {
                        ingredient.EmbeddingStatus = IngredientEmbeddingStatus.Error;
                    }
                }
                catch
                {
                    ingredient.EmbeddingStatus = IngredientEmbeddingStatus.Error;
                }
            }

            await context.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Individually processed {Success}/{Total} ingredients.", successCount, ingredients.Count);
            return successCount;
        }
    }

    private static string BuildIngredientText(Ingredient ingredient)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"Ingredient: {ingredient.CanonicalName}");

        var aliases = ingredient.Aliases
            .Select(a => a.AliasName)
            .Where(n => !string.IsNullOrWhiteSpace(n) &&
                        !n.Equals(ingredient.CanonicalName, StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .Take(10) // Limit aliases
            .ToList();

        if (aliases.Count > 0)
            sb.AppendLine($"Also known as: {string.Join(", ", aliases)}");

        return sb.ToString().Trim();
    }

    #endregion

    #region User Embedding

    public async Task<int> ProcessUserBatchAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var users = await context.Users
            .Include(u => u.Preferences)
            .ThenInclude(p => p.Tag)
            .Include(u => u.RecipeLikes)
            .ThenInclude(rl => rl.Recipe)
            .Where(u => u.EmbeddingStatus == UserEmbeddingStatus.Pending)
            .OrderBy(u => u.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (users.Count == 0)
            return 0;

        var texts = users.Select(BuildUserText).ToList();

        try
        {
            var embeddings = await provider.GenerateBatchEmbeddingsAsync(texts, cancellationToken);

            for (var i = 0; i < users.Count && i < embeddings.Count; i++)
            {
                users[i].Embedding = new Vector(embeddings[i]);
                users[i].EmbeddingStatus = UserEmbeddingStatus.Ready;
                users[i].EmbeddingUpdatedAt = DateTime.UtcNow;
                users[i].UpdatedAt = DateTime.UtcNow;
            }

            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Generated embeddings for {Count} users.", embeddings.Count);
            return embeddings.Count;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Batch embedding failed for users, trying individual processing.");

            var successCount = 0;
            foreach (var user in users)
            {
                try
                {
                    var embedding = await provider.GenerateEmbeddingAsync(BuildUserText(user), cancellationToken);
                    if (embedding.Length > 0)
                    {
                        user.Embedding = new Vector(embedding);
                        user.EmbeddingStatus = UserEmbeddingStatus.Ready;
                        user.EmbeddingUpdatedAt = DateTime.UtcNow;
                        user.UpdatedAt = DateTime.UtcNow;
                        successCount++;
                    }
                    else
                    {
                        user.EmbeddingStatus = UserEmbeddingStatus.Error;
                    }
                }
                catch
                {
                    user.EmbeddingStatus = UserEmbeddingStatus.Error;
                }
            }

            await context.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Individually processed {Success}/{Total} users.", successCount, users.Count);
            return successCount;
        }
    }

    private static string BuildUserText(User user)
    {
        var sb = new StringBuilder();
        sb.AppendLine("User food preferences:");

        // Group preferences by relation type
        var likes = user.Preferences
            .Where(p => p.Relation == UserPreferenceRelation.Like)
            .Select(p => p.Tag.Name)
            .ToList();

        var dislikes = user.Preferences
            .Where(p => p.Relation == UserPreferenceRelation.Dislike)
            .Select(p => p.Tag.Name)
            .ToList();

        var allergies = user.Preferences
            .Where(p => p.Relation == UserPreferenceRelation.Allergy)
            .Select(p => p.Tag.Name)
            .ToList();

        var restrictions = user.Preferences
            .Where(p => p.Relation == UserPreferenceRelation.Restriction)
            .Select(p => p.Tag.Name)
            .ToList();

        var goals = user.Preferences
            .Where(p => p.Relation == UserPreferenceRelation.Goal)
            .Select(p => p.Tag.Name)
            .ToList();

        if (likes.Count > 0)
            sb.AppendLine($"Likes: {string.Join(", ", likes)}");

        if (dislikes.Count > 0)
            sb.AppendLine($"Dislikes: {string.Join(", ", dislikes)}");

        if (allergies.Count > 0)
            sb.AppendLine($"Allergies: {string.Join(", ", allergies)}");

        if (restrictions.Count > 0)
            sb.AppendLine($"Dietary restrictions: {string.Join(", ", restrictions)}");

        if (goals.Count > 0)
            sb.AppendLine($"Health goals: {string.Join(", ", goals)}");

        // Recently liked recipes (up to 10)
        var likedRecipes = user.RecipeLikes
            .OrderByDescending(rl => rl.CreatedAt)
            .Take(10)
            .Select(rl => rl.Recipe.Title)
            .ToList();

        if (likedRecipes.Count > 0)
            sb.AppendLine($"Recently liked recipes: {string.Join(", ", likedRecipes)}");

        return sb.ToString().Trim();
    }

    #endregion
}