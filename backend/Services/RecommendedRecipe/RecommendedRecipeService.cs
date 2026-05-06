using backend.Data;
using backend.Extensions;
using backend.Interfaces;
using backend.Models;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

namespace backend.Services.RecommendedRecipe;

/// <summary>
/// Service for retrieving personalized recipe recommendations based on user preferences.
/// </summary>
public class RecommendedRecipeService : IRecommendedRecipeService, IDisposable
{
    private const string RecipeTagType = "recipe";
    private readonly AppDbContext _dbContext;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RecommendedRecipeService> _logger;

    /// <summary>
    /// Semaphore to limit concurrent background image generation tasks.
    /// </summary>
    private readonly SemaphoreSlim _imageGenerationSemaphore = new(3, 3);

    /// <summary>
    /// Tracks pending background tasks to ensure cleanup on disposal.
    /// </summary>
    private readonly List<Task> _pendingTasks = [];
    private readonly object _pendingTasksLock = new();

    public RecommendedRecipeService(
        AppDbContext dbContext,
        IServiceScopeFactory scopeFactory,
        ILogger<RecommendedRecipeService> logger)
    {
        _dbContext = dbContext;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<RecommendedRecipeResult> GetRecommendationsAsync(
        Guid userId,
        int limit = 20,
        int offset = 0,
        string? search = null,
        string? seed = null,
        CancellationToken cancellationToken = default)
    {
        // Get user with preferences
        var user = await _dbContext.Users
            .Include(u => u.Preferences)
            .ThenInclude(p => p.Tag)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null)
        {
            return new RecommendedRecipeResult(RecommendedRecipeResultStatus.UserNotFound,
                ErrorMessage: "User not found");
        }

        // Extract user's tag preferences by relation type
        var allergyTagIds = user.Preferences
            .Where(p => p.Relation == UserPreferenceRelation.Allergy)
            .Select(p => p.TagId)
            .ToHashSet();

        var restrictionTagIds = user.Preferences
            .Where(p => p.Relation == UserPreferenceRelation.Restriction)
            .Select(p => p.TagId)
            .ToHashSet();

        var preferenceTagIds = user.Preferences
            .Where(p => p.Relation == UserPreferenceRelation.Like ||
                        p.Relation == UserPreferenceRelation.Goal)
            .Select(p => p.TagId)
            .ToHashSet();

        var excludeTagIds = allergyTagIds.Union(restrictionTagIds).ToList();

        _logger.LogInformation(
            "Getting recommendations for user {UserId}: {ExcludeCount} exclude tags, {PreferenceCount} preference tags",
            userId, excludeTagIds.Count, preferenceTagIds.Count);

        var trimmedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        // Build query for System and User recipes
        var query = _dbContext.Recipes
            .Where(r => (r.Type == RecipeType.System || r.Type == RecipeType.User)
                     && r.Visibility == RecipeVisibility.Public);

        // Exclude recipes with allergy/restriction tags
        if (excludeTagIds.Count > 0)
        {
            query = query.Where(r => !r.Tags.Any(rt => excludeTagIds.Contains(rt.TagId)));
        }

        if (!string.IsNullOrWhiteSpace(trimmedSearch))
        {
            var searchPattern = $"%{trimmedSearch}%";
            query = query.Where(r =>
                EF.Functions.ILike(r.Title, searchPattern) ||
                r.Tags.Any(rt =>
                    rt.Tag.Type == RecipeTagType &&
                    rt.Tag.IsActive &&
                    (EF.Functions.ILike(rt.Tag.Name, searchPattern) ||
                     EF.Functions.ILike(rt.Tag.DisplayName, searchPattern))));
        }

        // Count total matching recipes
        var totalCount = await query.CountAsync(cancellationToken);

        if (totalCount == 0)
        {
            if (!string.IsNullOrWhiteSpace(trimmedSearch))
            {
                return new RecommendedRecipeResult(
                    RecommendedRecipeResultStatus.Success,
                    [],
                    0);
            }

            // Fallback: try embedding-based recommendations or popular recipes
            return await GetFallbackRecommendationsAsync(user, limit, offset, cancellationToken);
        }

        // Query with preference ranking and stable seed for pagination (defaults to hourly).
        var trimmedSeed = string.IsNullOrWhiteSpace(seed) ? null : seed.Trim();
        var orderingSeed = trimmedSeed == null
            ? $"{user.Id:N}-{DateTime.UtcNow:yyyyMMddHH}"
            : $"{user.Id:N}-{trimmedSeed}";
        var recipes = await query
            .Include(r => r.Tags)
            .Include(r => r.Author)
            .Select(r => new
            {
                Recipe = r,
                PreferenceMatchCount = preferenceTagIds.Count > 0
                    ? r.Tags.Count(rt => preferenceTagIds.Contains(rt.TagId))
                    : 0,
                HasImage = r.ImageUrls != null && r.ImageUrls.Count > 0
            })
            .OrderByDescending(x => x.PreferenceMatchCount)
            .ThenByDescending(x => x.HasImage)
            .ThenBy(x => PostgresFunctions.Md5(PostgresFunctions.Concat(x.Recipe.Id, orderingSeed)))
            .ThenByDescending(x => x.Recipe.LikesCount + x.Recipe.SavedCount)
            .ThenBy(x => x.Recipe.Id)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var dtos = await BuildDtosAsync(
            recipes.Select(x => (x.Recipe, x.PreferenceMatchCount)),
            user.Id,
            cancellationToken);

        _logger.LogInformation("Returning {Count} recommendations for user {UserId}", dtos.Count, userId);

        return new RecommendedRecipeResult(
            RecommendedRecipeResultStatus.Success,
            dtos,
            totalCount);
    }

    private async Task<RecommendedRecipeResult> GetFallbackRecommendationsAsync(
        User user,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Using fallback recommendations for user {UserId}", user.Id);

        // Try embedding-based similarity if user has embedding
        if (user.Embedding != null && user.EmbeddingStatus == UserEmbeddingStatus.Ready)
        {
            var embeddingRecipes = await _dbContext.Recipes
                .Where(r => (r.Type == RecipeType.System || r.Type == RecipeType.User) &&
                            r.Visibility == RecipeVisibility.Public &&
                            r.Embedding != null &&
                            r.EmbeddingStatus == RecipeEmbeddingStatus.Ready)
                .Include(r => r.Author)
                .OrderByDescending(r => r.ImageUrls != null && r.ImageUrls.Count > 0)
                .ThenBy(r => r.Embedding!.CosineDistance(user.Embedding))
                .Skip(offset)
                .Take(limit)
                .ToListAsync(cancellationToken);

            if (embeddingRecipes.Count > 0)
            {
                var totalEmbedding = await _dbContext.Recipes
                    .Where(r => (r.Type == RecipeType.System || r.Type == RecipeType.User) &&
                                r.Visibility == RecipeVisibility.Public &&
                                r.Embedding != null)
                    .CountAsync(cancellationToken);

                return new RecommendedRecipeResult(
                    RecommendedRecipeResultStatus.Success,
                    await BuildDtosAsync(
                        embeddingRecipes.Select(r => (r, 0)),
                        user.Id,
                        cancellationToken),
                    totalEmbedding);
            }
        }

        // Final fallback: popular System and User recipes
        var popularRecipes = await _dbContext.Recipes
            .Where(r => (r.Type == RecipeType.System || r.Type == RecipeType.User)
                     && r.Visibility == RecipeVisibility.Public)
            .Include(r => r.Author)
            .OrderByDescending(r => r.ImageUrls != null && r.ImageUrls.Count > 0)
            .ThenByDescending(r => r.LikesCount + r.SavedCount)
            .ThenByDescending(r => r.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);

        if (popularRecipes.Count == 0)
        {
            return new RecommendedRecipeResult(
                RecommendedRecipeResultStatus.NoRecipesAvailable,
                ErrorMessage: "No recipes available");
        }

        var totalPopular = await _dbContext.Recipes
            .Where(r => (r.Type == RecipeType.System || r.Type == RecipeType.User)
                     && r.Visibility == RecipeVisibility.Public)
            .CountAsync(cancellationToken);

        return new RecommendedRecipeResult(
            RecommendedRecipeResultStatus.Success,
            await BuildDtosAsync(
                popularRecipes.Select(r => (r, 0)),
                user.Id,
                cancellationToken),
            totalPopular);
    }

    private async Task<List<RecommendedRecipeDto>> BuildDtosAsync(
        IEnumerable<(Recipe Recipe, int PreferenceMatchCount)> recipes,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var recipeList = recipes.ToList();
        if (recipeList.Count == 0)
        {
            return [];
        }

        var recipeIds = recipeList.Select(item => item.Recipe.Id).ToList();
        var savedIds = await _dbContext.RecipeSaves
            .AsNoTracking()
            .Where(save => save.UserId == userId && recipeIds.Contains(save.RecipeId))
            .Select(save => save.RecipeId)
            .ToListAsync(cancellationToken);
        var savedIdSet = savedIds.ToHashSet();

        var tagsLookup = new Dictionary<Guid, List<string>>();
        if (recipeIds.Count > 0)
        {
            var tagEntries = await _dbContext.RecipeTags
                .AsNoTracking()
                .Where(tag => recipeIds.Contains(tag.RecipeId) &&
                              tag.Tag.Type == RecipeTagType &&
                              tag.Tag.IsActive)
                .Select(tag => new
                {
                    tag.RecipeId,
                    TagName = tag.Tag.DisplayName
                })
                .ToListAsync(cancellationToken);

            foreach (var group in tagEntries.GroupBy(entry => entry.RecipeId))
            {
                var orderedTags = group
                    .Select(entry => entry.TagName)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToList();

                tagsLookup[group.Key] = orderedTags;
            }
        }

        var dtos = new List<RecommendedRecipeDto>();
        var queuedRecipeIds = new HashSet<Guid>();
        foreach (var item in recipeList)
        {
            var coverImageUrl = item.Recipe.ImageUrls?.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(coverImageUrl) && queuedRecipeIds.Add(item.Recipe.Id))
            {
                QueueImageGeneration(item.Recipe.Id);
            }
            var savedByMe = savedIdSet.Contains(item.Recipe.Id);
            IReadOnlyList<string> tags = tagsLookup.TryGetValue(item.Recipe.Id, out var recipeTags)
                ? recipeTags
                : Array.Empty<string>();
            dtos.Add(MapToDto(
                item.Recipe,
                item.PreferenceMatchCount,
                coverImageUrl,
                savedByMe,
                tags));
        }

        return dtos;
    }

    /// <summary>
    /// Queues image generation for a recipe with bounded concurrency.
    /// Uses a semaphore to limit concurrent tasks and tracks tasks for cleanup.
    /// </summary>
    private void QueueImageGeneration(Guid recipeId)
    {
        var task = Task.Run(async () =>
        {
            // Wait for semaphore slot to limit concurrency
            await _imageGenerationSemaphore.WaitAsync();
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var imageService = scope.ServiceProvider.GetRequiredService<IRecipeImageService>();

                var recipe = await dbContext.Recipes
                    .Include(r => r.Ingredients)
                        .ThenInclude(ri => ri.Ingredient)
                    .Include(r => r.Tags)
                        .ThenInclude(rt => rt.Tag)
                    .FirstOrDefaultAsync(r => r.Id == recipeId);

                if (recipe == null)
                {
                    return;
                }

                await imageService.EnsureCoverImageUrlAsync(recipe, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate cover image for recipe {RecipeId} in background.", recipeId);
            }
            finally
            {
                _imageGenerationSemaphore.Release();
            }
        });

        // Track the task for cleanup
        lock (_pendingTasksLock)
        {
            // Clean up completed tasks
            _pendingTasks.RemoveAll(t => t.IsCompleted);
            _pendingTasks.Add(task);
        }
    }

    private static RecommendedRecipeDto MapToDto(
        Recipe recipe,
        int preferenceMatchCount,
        string? coverImageUrl,
        bool savedByMe,
        IReadOnlyList<string> tags)
    {
        return new RecommendedRecipeDto(
            RecipeId: recipe.Id,
            Title: recipe.Title.ToTitleCase(),
            Description: recipe.Description,
            CoverImageUrl: coverImageUrl ?? recipe.ImageUrls?.FirstOrDefault(),
            TotalTimeMinutes: recipe.TotalTimeMinutes,
            Difficulty: recipe.Difficulty,
            Servings: recipe.Servings,
            PreferenceMatchCount: preferenceMatchCount,
            LikesCount: recipe.LikesCount,
            SavedCount: recipe.SavedCount,
            SavedByMe: savedByMe,
            Tags: tags,
            Type: recipe.Type,
            AuthorId: recipe.AuthorId,
            AuthorNickname: recipe.Author?.Nickname,
            AuthorAvatarUrl: recipe.Author?.AvatarUrl);
    }

    public void Dispose()
    {
        // Wait for pending tasks to complete (with timeout)
        Task[] tasks;
        lock (_pendingTasksLock)
        {
            tasks = _pendingTasks.Where(t => !t.IsCompleted).ToArray();
            _pendingTasks.Clear();
        }

        if (tasks.Length > 0)
        {
            _logger.LogInformation("Waiting for {Count} pending image generation tasks to complete...", tasks.Length);
            Task.WaitAll(tasks, TimeSpan.FromSeconds(30));
        }

        _imageGenerationSemaphore.Dispose();
    }
}
