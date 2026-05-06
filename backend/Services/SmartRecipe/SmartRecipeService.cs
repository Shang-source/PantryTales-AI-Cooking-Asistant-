using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using backend.Data;
using backend.Dtos.SmartRecipes;
using backend.Extensions;
using backend.Interfaces;
using backend.Models;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

namespace backend.Services.SmartRecipe;

/// <summary>
/// Service for generating and retrieving AI-powered smart recipe suggestions.
/// </summary>
public class SmartRecipeService : ISmartRecipeService
{
    private readonly AppDbContext _dbContext;
    private readonly ISmartRecipeAIProvider _aiProvider;
    private readonly ILogger<SmartRecipeService> _logger;

    private const int MaxReferenceRecipes = 25;
    private const int TargetGeneratedRecipes = 7;
    private const int MinMissingOneOrTwoRecipes = 2;
    private const int MaxInventoryItemsForPrompt = 20;
    private const int MaxGenerationsPerDay = 3;

    private static readonly string[] BasicIngredients =
    [
        "water",
        "salt",
        "pepper",
        "black pepper",
        "oil",
        "vegetable oil",
        "olive oil",
        "cooking oil",
        "sugar",
        "flour",
        "all-purpose flour"
    ];

    public SmartRecipeService(
        AppDbContext dbContext,
        ISmartRecipeAIProvider aiProvider,
        ILogger<SmartRecipeService> logger)
    {
        _dbContext = dbContext;
        _aiProvider = aiProvider;
        _logger = logger;
    }

    public async Task<SmartRecipeResult> GetOrGenerateAsync(
        Guid userId,
        bool allowStale = false,
        CancellationToken cancellationToken = default)
    {
        // Get user and their household
        var user = await _dbContext.Users
            .Include(u => u.Preferences)
                .ThenInclude(p => p.Tag)
            .Include(u => u.HouseholdMemberships)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null)
            return new SmartRecipeResult(SmartRecipeResultStatus.UserNotFound, ErrorMessage: "User not found");

        // Get user's primary household (needed for shared inventory)
        var householdMembership = user.HouseholdMemberships.FirstOrDefault();
        if (householdMembership == null)
            return new SmartRecipeResult(SmartRecipeResultStatus.NoHousehold, ErrorMessage: "User has no household");

        var householdId = householdMembership.HouseholdId;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Get current inventory (shared per household) and calculate hash
        var inventory = await GetInventoryAsync(householdId, cancellationToken);
        if (inventory.Count == 0)
            return new SmartRecipeResult(SmartRecipeResultStatus.NoInventory, ErrorMessage: "No items in inventory");

        var inventoryHash = ComputeInventoryHash(inventory);

        // Check for existing smart recipes for this user today
        var existingRecipes = await _dbContext.SmartRecipes
            .AsNoTracking()
            .Where(sr => sr.UserId == userId && sr.GeneratedDate == today)
            .Include(sr => sr.Recipe)
                .ThenInclude(r => r.Ingredients)
                    .ThenInclude(ri => ri.Ingredient)
                        .ThenInclude(i => i!.Tags)
                            .ThenInclude(it => it.Tag)
            .OrderBy(sr => sr.MissingIngredientsCount)
            .ThenByDescending(sr => sr.MatchScore)
            .ToListAsync(cancellationToken);

        // If recipes exist and inventory hasn't changed, return cached
        if (existingRecipes.Count > 0)
        {
            var firstHash = existingRecipes.First().InventorySnapshotHash;
            if (firstHash == inventoryHash)
            {
                _logger.LogInformation("Returning cached smart recipes for user {UserId}", userId);
                return MapToResult(existingRecipes);
            }

            // Inventory changed - delete old and regenerate
            // Use ExecuteDeleteAsync to avoid tracking conflicts with AsNoTracking-loaded entities
            _logger.LogInformation("Inventory changed for user {UserId}, regenerating", userId);

            var oldRecipeIds = existingRecipes.Select(sr => sr.RecipeId).ToList();

            // Delete in order: RecipeIngredients -> SmartRecipes -> Recipes
            await _dbContext.RecipeIngredients
                .Where(ri => oldRecipeIds.Contains(ri.RecipeId))
                .ExecuteDeleteAsync(cancellationToken);

            await _dbContext.SmartRecipes
                .Where(sr => sr.UserId == userId && sr.GeneratedDate == today)
                .ExecuteDeleteAsync(cancellationToken);

            // Delete orphaned Recipe entities (Model type only)
            await _dbContext.Recipes
                .Where(r => oldRecipeIds.Contains(r.Id) && r.Type == RecipeType.Model)
                .ExecuteDeleteAsync(cancellationToken);
        }

        // If caller allows stale recipes, return most recent batch instead of auto-generating
        if (allowStale)
        {
            var latestDate = await _dbContext.SmartRecipes
                .AsNoTracking()
                .Where(sr => sr.UserId == userId)
                .OrderByDescending(sr => sr.GeneratedDate)
                .Select(sr => (DateOnly?)sr.GeneratedDate)
                .FirstOrDefaultAsync(cancellationToken);

            if (latestDate.HasValue)
            {
                var latestRecipes = await _dbContext.SmartRecipes
                    .AsNoTracking()
                    .Where(sr => sr.UserId == userId && sr.GeneratedDate == latestDate.Value)
                    .Include(sr => sr.Recipe)
                        .ThenInclude(r => r.Ingredients)
                            .ThenInclude(ri => ri.Ingredient)
                                .ThenInclude(i => i!.Tags)
                                    .ThenInclude(it => it.Tag)
                    .OrderBy(sr => sr.MissingIngredientsCount)
                    .ThenByDescending(sr => sr.MatchScore)
                    .ToListAsync(cancellationToken);

                if (latestRecipes.Count > 0)
                {
                    return MapToResult(latestRecipes);
                }
            }

            return new SmartRecipeResult(SmartRecipeResultStatus.Success, []);
        }

        // Generate new recipes (use household size as default servings)
        return await GenerateSmartRecipesAsync(user, householdId, inventory, inventoryHash, today, null, cancellationToken);
    }

    public async Task<SmartRecipeResult> ForceRegenerateAsync(Guid userId, int? servings = null, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .Include(u => u.Preferences)
                .ThenInclude(p => p.Tag)
            .Include(u => u.HouseholdMemberships)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null)
            return new SmartRecipeResult(SmartRecipeResultStatus.UserNotFound, ErrorMessage: "User not found");

        var householdMembership = user.HouseholdMemberships.FirstOrDefault();
        if (householdMembership == null)
            return new SmartRecipeResult(SmartRecipeResultStatus.NoHousehold, ErrorMessage: "User has no household");

        var householdId = householdMembership.HouseholdId;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Get inventory from shared household
        var inventory = await GetInventoryAsync(householdId, cancellationToken);
        if (inventory.Count == 0)
            return new SmartRecipeResult(SmartRecipeResultStatus.NoInventory, ErrorMessage: "No items in inventory");

        var inventoryHash = ComputeInventoryHash(inventory);

        // Delete existing for this user today using bulk operations to avoid tracking conflicts
        // First get the recipe IDs to delete their ingredients
        var recipeIds = await _dbContext.SmartRecipes
            .AsNoTracking()
            .Where(sr => sr.UserId == userId && sr.GeneratedDate == today)
            .Select(sr => sr.RecipeId)
            .ToListAsync(cancellationToken);

        if (recipeIds.Count > 0)
        {
            // Delete associated recipe ingredients using bulk delete
            await _dbContext.RecipeIngredients
                .Where(ri => recipeIds.Contains(ri.RecipeId))
                .ExecuteDeleteAsync(cancellationToken);

            // Delete smart recipes using bulk delete
            await _dbContext.SmartRecipes
                .Where(sr => sr.UserId == userId && sr.GeneratedDate == today)
                .ExecuteDeleteAsync(cancellationToken);

            // Delete the orphaned Recipe entities (Model type only)
            await _dbContext.Recipes
                .Where(r => recipeIds.Contains(r.Id) && r.Type == RecipeType.Model)
                .ExecuteDeleteAsync(cancellationToken);
        }

        return await GenerateSmartRecipesAsync(user, householdId, inventory, inventoryHash, today, servings, cancellationToken);
    }

    public async Task InvalidateForHouseholdAsync(Guid householdId, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Get all user IDs in this household
        var userIds = await _dbContext.HouseholdMembers
            .AsNoTracking()
            .Where(m => m.HouseholdId == householdId)
            .Select(m => m.UserId)
            .ToListAsync(cancellationToken);

        if (userIds.Count == 0)
            return;

        // Mark today's recipes for all household users as stale by clearing the hash
        // This will trigger regeneration on next access
        var todayRecipes = await _dbContext.SmartRecipes
            .Where(sr => userIds.Contains(sr.UserId) && sr.GeneratedDate == today)
            .ToListAsync(cancellationToken);

        if (todayRecipes.Count > 0)
        {
            foreach (var recipe in todayRecipes)
            {
                recipe.InventorySnapshotHash = null; // Force regeneration
            }
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Invalidated smart recipes for {Count} users in household {HouseholdId}",
                userIds.Count, householdId);
        }
    }

    public async IAsyncEnumerable<SmartRecipeSseEvent> StreamGenerateAsync(
        Guid userId,
        int? servings = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Get user and their household
        var user = await _dbContext.Users
            .Include(u => u.Preferences)
                .ThenInclude(p => p.Tag)
            .Include(u => u.HouseholdMemberships)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null)
        {
            yield return new SmartRecipeSseEvent(SmartRecipeSseEventType.Error, ErrorMessage: "User not found");
            yield break;
        }

        var householdMembership = user.HouseholdMemberships.FirstOrDefault();
        if (householdMembership == null)
        {
            yield return new SmartRecipeSseEvent(SmartRecipeSseEventType.Error, ErrorMessage: "User has no household");
            yield break;
        }

        var householdId = householdMembership.HouseholdId;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var todayStart = today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var todayEnd = today.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        // Check daily generation limit and log atomically using a transaction
        SmartRecipeSseEvent? limitCheckError = null;
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var generationsToday = await _dbContext.SmartRecipeGenerationLogs
                .CountAsync(log => log.UserId == userId && log.GeneratedAt >= todayStart && log.GeneratedAt < todayEnd, cancellationToken);

            if (generationsToday >= MaxGenerationsPerDay)
            {
                _logger.LogWarning("User {UserId} has reached daily generation limit ({Count}/{Max})", userId, generationsToday, MaxGenerationsPerDay);
                limitCheckError = new SmartRecipeSseEvent(SmartRecipeSseEventType.Error,
                    ErrorMessage: $"Daily limit reached. You can generate recipes {MaxGenerationsPerDay} times per day. Please try again tomorrow.");
            }
            else
            {
                // Log this generation attempt
                _dbContext.SmartRecipeGenerationLogs.Add(new SmartRecipeGenerationLog { UserId = userId });
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to log generation attempt for user {UserId}", userId);
            limitCheckError = new SmartRecipeSseEvent(SmartRecipeSseEventType.Error, ErrorMessage: "Failed to process request. Please try again.");
        }

        if (limitCheckError != null)
        {
            yield return limitCheckError;
            yield break;
        }

        // Get inventory
        var inventory = await GetInventoryAsync(householdId, cancellationToken);
        if (inventory.Count == 0)
        {
            yield return new SmartRecipeSseEvent(SmartRecipeSseEventType.Error, ErrorMessage: "No items in inventory");
            yield break;
        }

        var inventoryHash = ComputeInventoryHash(inventory);

        // Delete existing recipes for today (if any)
        var existingRecipeIds = await _dbContext.SmartRecipes
            .AsNoTracking()
            .Where(sr => sr.UserId == userId && sr.GeneratedDate == today)
            .Select(sr => sr.RecipeId)
            .ToListAsync(cancellationToken);

        if (existingRecipeIds.Count > 0)
        {
            await _dbContext.RecipeIngredients
                .Where(ri => existingRecipeIds.Contains(ri.RecipeId))
                .ExecuteDeleteAsync(cancellationToken);

            await _dbContext.SmartRecipes
                .Where(sr => sr.UserId == userId && sr.GeneratedDate == today)
                .ExecuteDeleteAsync(cancellationToken);

            await _dbContext.Recipes
                .Where(r => existingRecipeIds.Contains(r.Id) && r.Type == RecipeType.Model)
                .ExecuteDeleteAsync(cancellationToken);
        }

        // Get user preferences
        var allergies = user.Preferences
            .Where(p => p.Relation == UserPreferenceRelation.Allergy || p.Relation == UserPreferenceRelation.Restriction)
            .Select(p => p.Tag.Name)
            .ToList();

        var preferences = user.Preferences
            .Where(p => p.Relation == UserPreferenceRelation.Like || p.Relation == UserPreferenceRelation.Goal)
            .Select(p => p.Tag.Name)
            .ToList();

        // Determine servings count
        int servingsCount;
        if (servings.HasValue && servings.Value > 0)
        {
            servingsCount = servings.Value;
        }
        else
        {
            var householdSize = await _dbContext.HouseholdMembers
                .CountAsync(m => m.HouseholdId == householdId, cancellationToken);
            servingsCount = Math.Max(1, householdSize);
        }

        // Get reference recipes for context
        var inventoryIngredientTagIds = await GetInventoryIngredientTagIdsAsync(householdId, cancellationToken);
        var referenceRecipes = await QueryReferenceRecipesAsync(
            allergies, preferences, inventoryIngredientTagIds, user.Embedding, cancellationToken);

        // Build AI request
        var inventoryItemsForPrompt = inventory
            .Select(i => $"{i.Name} {i.Amount} {i.Unit}".Trim())
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .Take(MaxInventoryItemsForPrompt)
            .ToList();

        var request = new SmartRecipeAIRequest(
            InventoryItems: inventoryItemsForPrompt,
            ServingsCount: servingsCount,
            Preferences: preferences,
            Avoid: allergies,
            ReferenceRecipes: referenceRecipes
        );

        var inventoryNames = inventory
            .Select(i => NormalizeIngredientName(i.Name))
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();

        // Yield start event
        yield return new SmartRecipeSseEvent(SmartRecipeSseEventType.Start, TotalExpected: TargetGeneratedRecipes);

        var previousTitles = new List<string>();
        var successCount = 0;

        // Generate recipes one by one
        // First 5 recipes: target 0 missing ingredients ("Can Make")
        // Last 2 recipes: target 1-2 missing ingredients ("Missing 1-2")
        for (int i = 0; i < TargetGeneratedRecipes; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var missingTarget = i < 5 ? "zero" : "one-to-two";
            SmartRecipeSseEvent? eventToYield = null;

            try
            {
                var aiResponse = await _aiProvider.GenerateSingleRecipeAsync(
                    request, i, previousTitles, missingTarget, cancellationToken);

                if (!aiResponse.Success || aiResponse.Recipes == null || aiResponse.Recipes.Count == 0)
                {
                    _logger.LogWarning("Failed to generate recipe at index {Index}: {Error}", i, aiResponse.ErrorMessage);
                    continue;
                }

                var generatedRecipe = aiResponse.Recipes[0];
                previousTitles.Add(generatedRecipe.Title);

                // Calculate missing ingredients
                var missingIngredients = MergeMissingIngredients(generatedRecipe, inventoryNames);

                // Save to database
                var savedDto = await SaveSingleGeneratedRecipeAsync(
                    user, householdId, generatedRecipe, missingIngredients, inventoryHash, today, cancellationToken);

                if (savedDto != null)
                {
                    successCount++;
                    eventToYield = new SmartRecipeSseEvent(
                        SmartRecipeSseEventType.Recipe,
                        Recipe: savedDto,
                        CurrentIndex: successCount,
                        TotalExpected: TargetGeneratedRecipes);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating recipe at index {Index}", i);
                // Continue with next recipe
            }

            // Yield outside of try-catch block (C# requirement)
            if (eventToYield != null)
            {
                yield return eventToYield;
            }
        }

        _logger.LogInformation("Streaming generation complete: {Count} recipes for user {UserId}", successCount, userId);

        // Yield complete event
        yield return new SmartRecipeSseEvent(SmartRecipeSseEventType.Complete, CurrentIndex: successCount, TotalExpected: TargetGeneratedRecipes);
    }

    private async Task<SmartRecipeDto?> SaveSingleGeneratedRecipeAsync(
        User user,
        Guid householdId,
        GeneratedRecipe recipeData,
        List<string> missingIngredients,
        string inventoryHash,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        // Build ingredients list for description
        var ingredientsList = string.Join("\n", recipeData.Ingredients.Select(ing =>
            $"• {ing.Amount} {ing.Unit} {ing.Name}"));

        var fullDescription = recipeData.Description;
        if (!string.IsNullOrEmpty(ingredientsList))
        {
            fullDescription = $"{recipeData.Description}\n\n**Ingredients:**\n{ingredientsList}";
        }

        // Create the recipe
        var recipe = new Recipe
        {
            HouseholdId = householdId,
            Type = RecipeType.Model,
            AuthorId = null,
            Title = recipeData.Title.ToTitleCase(),
            Description = fullDescription,
            Servings = recipeData.Servings,
            TotalTimeMinutes = recipeData.TotalTimeMinutes,
            Difficulty = ParseDifficulty(recipeData.Difficulty),
            Steps = JsonSerializer.Serialize(
                recipeData.Steps.OrderBy(s => s.Order).Select(s => s.Instruction).ToList(),
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
            Visibility = RecipeVisibility.Private,
            Calories = recipeData.Calories,
            Carbohydrates = recipeData.Carbohydrates,
            Fat = recipeData.Fat,
            Protein = recipeData.Protein,
            Sugar = recipeData.Sugar,
            Sodium = recipeData.Sodium,
            SaturatedFat = recipeData.SaturatedFat
        };

        _dbContext.Recipes.Add(recipe);

        // Get all ingredient names for this recipe
        var ingredientNames = recipeData.Ingredients
            .Select(i => i.Name.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();

        // Pre-load existing ingredients with their tags
        var existingIngredients = await _dbContext.Ingredients
            .Include(i => i.Tags)
            .Where(i => ingredientNames.Contains(i.CanonicalName.ToLower()))
            .ToDictionaryAsync(i => i.CanonicalName.ToLowerInvariant(), cancellationToken);

        // Pre-load ingredient_type tags for category assignment
        var categoryTags = await _dbContext.Tags
            .AsNoTracking()
            .Where(t => t.Type == "ingredient_type" && t.IsActive)
            .ToDictionaryAsync(t => t.DisplayName.ToLowerInvariant(), cancellationToken);

        // Create RecipeIngredient records
        foreach (var genIngredient in recipeData.Ingredients)
        {
            var normalizedName = genIngredient.Name.Trim().ToLowerInvariant();
            Guid ingredientId;
            Ingredient? ingredient = null;

            if (existingIngredients.TryGetValue(normalizedName, out var existingIngredient))
            {
                ingredientId = existingIngredient.Id;
                ingredient = existingIngredient;
            }
            else
            {
                var createdIngredient = new Ingredient
                {
                    CanonicalName = genIngredient.Name.Trim(),
                    DefaultUnit = genIngredient.Unit
                };
                _dbContext.Ingredients.Add(createdIngredient);
                existingIngredients[normalizedName] = createdIngredient;
                ingredientId = createdIngredient.Id;
                ingredient = createdIngredient;
            }

            // Assign category tag if AI provided a category and tag exists
            if (!string.IsNullOrEmpty(genIngredient.Category) && ingredient != null)
            {
                var categoryKey = genIngredient.Category.ToLowerInvariant();
                if (categoryTags.TryGetValue(categoryKey, out var categoryTag))
                {
                    // Check if this ingredient already has this tag
                    var hasTag = ingredient.Tags?.Any(t => t.TagId == categoryTag.Id) ?? false;
                    if (!hasTag)
                    {
                        var ingredientTag = new IngredientTag
                        {
                            IngredientId = ingredientId,
                            TagId = categoryTag.Id
                        };
                        _dbContext.Set<IngredientTag>().Add(ingredientTag);
                    }
                }
            }

            var recipeIngredient = new Models.RecipeIngredient
            {
                RecipeId = recipe.Id,
                IngredientId = ingredientId,
                Amount = genIngredient.Amount,
                Unit = genIngredient.Unit,
                IsOptional = false
            };
            _dbContext.RecipeIngredients.Add(recipeIngredient);
        }

        // Create smart recipe link
        var smartRecipe = new Models.SmartRecipe
        {
            UserId = user.Id,
            RecipeId = recipe.Id,
            GeneratedDate = today,
            MissingIngredientsCount = missingIngredients.Count,
            MissingIngredients = JsonSerializer.Serialize(missingIngredients),
            MatchScore = recipeData.MatchScore,
            InventorySnapshotHash = inventoryHash
        };

        _dbContext.SmartRecipes.Add(smartRecipe);

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Build and return DTO
        return new SmartRecipeDto(
            smartRecipe.Id,
            recipe.Id,
            recipe.Title,
            recipe.Description,
            recipe.ImageUrls?.FirstOrDefault(),
            recipe.TotalTimeMinutes,
            recipe.Difficulty,
            recipe.Servings,
            missingIngredients.Count,
            missingIngredients,
            recipeData.MatchScore,
            smartRecipe.GeneratedDate,
            smartRecipe.CreatedAt,
            recipeData.Ingredients.Select(i => new SmartRecipeIngredientDto(
                i.Name,
                i.Amount,
                i.Unit,
                false,
                i.Category
            )).ToList()
        );
    }

    private async Task<SmartRecipeResult> GenerateSmartRecipesAsync(
        User user,
        Guid householdId,
        List<InventoryItemInfo> inventory,
        string inventoryHash,
        DateOnly today,
        int? customServings,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Generating smart recipes for user {UserId} with custom servings {Servings}", user.Id, customServings);

        // Get user preferences
        var allergies = user.Preferences
            .Where(p => p.Relation == UserPreferenceRelation.Allergy || p.Relation == UserPreferenceRelation.Restriction)
            .Select(p => p.Tag.Name)
            .ToList();

        var preferences = user.Preferences
            .Where(p => p.Relation == UserPreferenceRelation.Like || p.Relation == UserPreferenceRelation.Goal)
            .Select(p => p.Tag.Name)
            .ToList();

        // Use custom servings if provided, otherwise default to household size
        int servingsCount;
        if (customServings.HasValue && customServings.Value > 0)
        {
            servingsCount = customServings.Value;
        }
        else
        {
            var householdSize = await _dbContext.HouseholdMembers
                .CountAsync(m => m.HouseholdId == householdId, cancellationToken);
            servingsCount = Math.Max(1, householdSize);
        }

        // Get ingredient tags from user's inventory for semantic matching
        var inventoryIngredientTagIds = await GetInventoryIngredientTagIdsAsync(householdId, cancellationToken);

        // Query reference recipes (with ingredient-level filtering)
        var referenceRecipes = await QueryReferenceRecipesAsync(
            allergies, preferences, inventoryIngredientTagIds, user.Embedding, cancellationToken);

        if (referenceRecipes.Count == 0)
        {
            _logger.LogWarning("No reference recipes found for user {UserId}", user.Id);
        }

        // Build AI request
        var inventoryItemsForPrompt = inventory
            .Select(i => $"{i.Name} {i.Amount} {i.Unit}".Trim())
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .Take(MaxInventoryItemsForPrompt)
            .ToList();

        var request = new SmartRecipeAIRequest(
            InventoryItems: inventoryItemsForPrompt,
            ServingsCount: servingsCount,
            Preferences: preferences,
            Avoid: allergies,
            ReferenceRecipes: referenceRecipes
        );

        // Call AI
        var aiResponse = await _aiProvider.GenerateRecipesAsync(request, cancellationToken);

        if (!aiResponse.Success || aiResponse.Recipes == null || aiResponse.Recipes.Count == 0)
        {
            _logger.LogError("AI generation failed: {Error}", aiResponse.ErrorMessage);
            return new SmartRecipeResult(SmartRecipeResultStatus.GenerationFailed,
                ErrorMessage: aiResponse.ErrorMessage ?? "AI generation failed");
        }

        var inventoryNames = inventory
            .Select(i => NormalizeIngredientName(i.Name))
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();

        var recipesWithMissing = aiResponse.Recipes
            .Select(r => new GeneratedRecipeWithMissing(
                r,
                MergeMissingIngredients(r, inventoryNames)))
            .ToList();

        var selectedRecipes = SelectBalancedRecipes(
            recipesWithMissing,
            TargetGeneratedRecipes,
            MinMissingOneOrTwoRecipes);

        // Collect all unique ingredient names from AI response
        var allIngredientNames = selectedRecipes
            .SelectMany(r => r.Recipe.Ingredients)
            .Select(i => i.Name.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();

        // Pre-load existing ingredients by name (AsNoTracking since we only need IDs)
        var existingIngredients = await _dbContext.Ingredients
            .AsNoTracking()
            .Where(i => allIngredientNames.Contains(i.CanonicalName.ToLower()))
            .ToDictionaryAsync(i => i.CanonicalName.ToLowerInvariant(), cancellationToken);

        // Track newly created ingredients in memory
        var newIngredients = new Dictionary<string, Ingredient>();

        // Save generated recipes
        var smartRecipes = new List<Models.SmartRecipe>();

        foreach (var generated in selectedRecipes)
        {
            var recipeData = generated.Recipe;
            var missingIngredients = generated.MissingIngredients;

            // Build ingredients list for description
            var ingredientsList = string.Join("\n", recipeData.Ingredients.Select(ing =>
                $"• {ing.Amount} {ing.Unit} {ing.Name}"));

            var fullDescription = recipeData.Description;
            if (!string.IsNullOrEmpty(ingredientsList))
            {
                fullDescription = $"{recipeData.Description}\n\n**Ingredients:**\n{ingredientsList}";
            }

            // Create the recipe in the database
            var recipe = new Recipe
            {
                HouseholdId = householdId,
                Type = RecipeType.Model,
                AuthorId = null,
                Title = recipeData.Title.ToTitleCase(),
                Description = fullDescription,
                Servings = recipeData.Servings,
                TotalTimeMinutes = recipeData.TotalTimeMinutes,
                Difficulty = ParseDifficulty(recipeData.Difficulty),
                // Store steps as List<string> (just the instructions) to match DeserializeSteps format
                Steps = JsonSerializer.Serialize(
                    recipeData.Steps.OrderBy(s => s.Order).Select(s => s.Instruction).ToList(),
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                Visibility = RecipeVisibility.Private,
                // Nutrition from AI estimation
                Calories = recipeData.Calories,
                Carbohydrates = recipeData.Carbohydrates,
                Fat = recipeData.Fat,
                Protein = recipeData.Protein,
                Sugar = recipeData.Sugar,
                Sodium = recipeData.Sodium,
                SaturatedFat = recipeData.SaturatedFat
            };

            _dbContext.Recipes.Add(recipe);

            // Create RecipeIngredient records for each ingredient
            foreach (var genIngredient in recipeData.Ingredients)
            {
                var normalizedName = genIngredient.Name.Trim().ToLowerInvariant();
                Guid ingredientId;

                // Check existing ingredients from DB
                if (existingIngredients.TryGetValue(normalizedName, out var existingIngredient))
                {
                    ingredientId = existingIngredient.Id;
                }
                // Check newly created ingredients in this batch
                else if (newIngredients.TryGetValue(normalizedName, out var newIngredient))
                {
                    ingredientId = newIngredient.Id;
                }
                else
                {
                    // Create new ingredient for AI-generated name
                    var createdIngredient = new Ingredient
                    {
                        CanonicalName = genIngredient.Name.Trim(),
                        DefaultUnit = genIngredient.Unit
                    };
                    _dbContext.Ingredients.Add(createdIngredient);
                    newIngredients[normalizedName] = createdIngredient;
                    ingredientId = createdIngredient.Id;
                }

                var recipeIngredient = new Models.RecipeIngredient
                {
                    RecipeId = recipe.Id,
                    IngredientId = ingredientId,
                    Amount = genIngredient.Amount,
                    Unit = genIngredient.Unit,
                    IsOptional = false
                };
                _dbContext.RecipeIngredients.Add(recipeIngredient);
            }

            // Create smart recipe link (per-user, not shared with household)
            var smartRecipe = new Models.SmartRecipe
            {
                UserId = user.Id,
                RecipeId = recipe.Id,
                GeneratedDate = today,
                MissingIngredientsCount = missingIngredients.Count,
                MissingIngredients = JsonSerializer.Serialize(missingIngredients),
                MatchScore = recipeData.MatchScore,
                InventorySnapshotHash = inventoryHash
            };

            _dbContext.SmartRecipes.Add(smartRecipe);
            smartRecipes.Add(smartRecipe);
        }

        // Batch save all recipes and smart recipes at once
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Generated {Count} smart recipes for user {UserId}",
                smartRecipes.Count, user.Id);

        // Reload with recipe data for return
        var savedRecipes = await _dbContext.SmartRecipes
            .AsNoTracking()
            .Where(sr => sr.UserId == user.Id && sr.GeneratedDate == today)
            .Include(sr => sr.Recipe)
                .ThenInclude(r => r.Ingredients)
                    .ThenInclude(ri => ri.Ingredient)
                        .ThenInclude(i => i!.Tags)
                            .ThenInclude(it => it.Tag)
            .OrderBy(sr => sr.MissingIngredientsCount)
            .ThenByDescending(sr => sr.MatchScore)
            .ToListAsync(cancellationToken);

        return MapToResult(savedRecipes);
    }

    private async Task<List<SmartRecipeReference>> QueryReferenceRecipesAsync(
        List<string> allergies,
        List<string> preferences,
        HashSet<int> inventoryIngredientTagIds,
        Pgvector.Vector? userEmbedding,
        CancellationToken cancellationToken)
    {
        // Try SQL query first (project only required fields)
        var query = _dbContext.Recipes
            .AsNoTracking()
            .Where(r => r.Visibility == RecipeVisibility.Public)
            .Where(r => r.Type == RecipeType.User || r.Type == RecipeType.System)
            .AsQueryable();

        // Exclude recipes with allergens (recipe-level tags)
        if (allergies.Count > 0)
        {
            query = query.Where(r => !r.Tags.Any(t => allergies.Contains(t.Tag.Name)));

            // Also exclude recipes containing ingredients tagged with allergen tags
            // This catches recipes that use allergen ingredients but aren't explicitly tagged
            query = query.Where(r => !r.Ingredients.Any(ri =>
                ri.Ingredient.Tags.Any(it => allergies.Contains(it.Tag.Name))));
        }

        // Prefer recipes with matching preferences and inventory ingredient tags
        var hasPreferenceFilters = preferences.Count > 0 || inventoryIngredientTagIds.Count > 0;
        query = hasPreferenceFilters
            ? query
                .OrderByDescending(r =>
                    // Count recipe-level preference tag matches
                    r.Tags.Count(t => preferences.Contains(t.Tag.Name)) +
                    // Count ingredient-level preference tag matches
                    r.Ingredients.Count(ri => ri.Ingredient.Tags.Any(it => preferences.Contains(it.Tag.Name))) +
                    // Count ingredients that share tags with user's inventory (semantic similarity)
                    r.Ingredients.Count(ri => ri.Ingredient.Tags.Any(it => inventoryIngredientTagIds.Contains(it.TagId))))
                .ThenByDescending(r => r.LikesCount + r.SavedCount)
            : query.OrderByDescending(r => r.LikesCount + r.SavedCount);

        var recipes = await query
            .Select(r => new ReferenceRecipeRow(
                r.Id,
                r.Title,
                r.Description,
                r.Ingredients
                    .Select(i => i.Ingredient != null ? i.Ingredient.CanonicalName : "")
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .ToList(),
                r.Tags
                    .Select(t => t.Tag.Name)
                    .ToList()
            ))
            .Take(MaxReferenceRecipes)
            .ToListAsync(cancellationToken);

        // If not enough recipes, use embedding similarity as fallback
        if (recipes.Count < 20 && userEmbedding != null)
        {
            _logger.LogInformation("Using embedding fallback for reference recipes");

            var excludeIds = recipes.Select(r => r.Id).ToList();
            var embeddingRecipes = await _dbContext.Recipes
                .AsNoTracking()
                .Where(r => r.Visibility == RecipeVisibility.Public)
                .Where(r => r.EmbeddingStatus == RecipeEmbeddingStatus.Ready)
                .Where(r => !excludeIds.Contains(r.Id))
                .OrderBy(r => r.Embedding!.L2Distance(userEmbedding))
                .Select(r => new ReferenceRecipeRow(
                    r.Id,
                    r.Title,
                    r.Description,
                    r.Ingredients
                        .Select(i => i.Ingredient != null ? i.Ingredient.CanonicalName : "")
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .ToList(),
                    r.Tags
                        .Select(t => t.Tag.Name)
                        .ToList()
                ))
                .Take(MaxReferenceRecipes - recipes.Count)
                .ToListAsync(cancellationToken);

            recipes.AddRange(embeddingRecipes);
        }

        return recipes.Select(r => new SmartRecipeReference(
            r.Title,
            r.Description,
            r.Ingredients,
            r.Tags
        )).ToList();
    }

    private async Task<List<InventoryItemInfo>> GetInventoryAsync(Guid householdId, CancellationToken cancellationToken)
    {
        return await _dbContext.InventoryItems
            .AsNoTracking()
            .Where(i => i.HouseholdId == householdId && i.Status == InventoryItemStatus.Active)
            .Select(i => new InventoryItemInfo(i.Name, i.Amount, i.Unit))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets all tag IDs associated with the ingredients in the user's inventory.
    /// This enables semantic matching - recipes with ingredients in similar categories
    /// (e.g., "meat", "dairy") will be preferred.
    /// </summary>
    private async Task<HashSet<int>> GetInventoryIngredientTagIdsAsync(Guid householdId, CancellationToken cancellationToken)
    {
        var tagIds = await _dbContext.InventoryItems
            .AsNoTracking()
            .Where(i => i.HouseholdId == householdId &&
                       i.Status == InventoryItemStatus.Active &&
                       i.IngredientId != null)
            .SelectMany(i => i.Ingredient!.Tags.Select(t => t.TagId))
            .Distinct()
            .ToListAsync(cancellationToken);

        return tagIds.ToHashSet();
    }

    private static string ComputeInventoryHash(List<InventoryItemInfo> inventory)
    {
        var sorted = inventory.OrderBy(i => i.Name).ToList();
        var json = JsonSerializer.Serialize(sorted);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes);
    }

    private static SmartRecipeResult MapToResult(List<Models.SmartRecipe> recipes)
    {
        var dtos = recipes.Select(sr => new SmartRecipeDto(
            sr.Id,
            sr.RecipeId,
            sr.Recipe.Title,
            sr.Recipe.Description,
            sr.Recipe.ImageUrls?.FirstOrDefault(),
            sr.Recipe.TotalTimeMinutes,
            sr.Recipe.Difficulty,
            sr.Recipe.Servings,
            sr.MissingIngredientsCount,
            string.IsNullOrEmpty(sr.MissingIngredients)
                ? Array.Empty<string>()
                : JsonSerializer.Deserialize<List<string>>(sr.MissingIngredients) ?? [],
            sr.MatchScore,
            sr.GeneratedDate,
            sr.CreatedAt,
            sr.Recipe.Ingredients.Select(ri => new SmartRecipeIngredientDto(
                ri.Ingredient?.CanonicalName ?? "Unknown",
                ri.Amount,
                ri.Unit,
                ri.IsOptional,
                // Get category from ingredient's "ingredient_type" tag if available
                ri.Ingredient?.Tags
                    .FirstOrDefault(t => t.Tag.Type == "ingredient_type")
                    ?.Tag.DisplayName
            )).ToList()
        )).ToList();

        return new SmartRecipeResult(SmartRecipeResultStatus.Success, dtos);
    }

    private static RecipeDifficulty ParseDifficulty(string difficulty)
    {
        return difficulty?.ToLowerInvariant() switch
        {
            "easy" => RecipeDifficulty.Easy,
            "medium" => RecipeDifficulty.Medium,
            "hard" => RecipeDifficulty.Hard,
            _ => RecipeDifficulty.None
        };
    }

    private record InventoryItemInfo(string Name, decimal Amount, string Unit);

    private static List<GeneratedRecipeWithMissing> SelectBalancedRecipes(
        IReadOnlyList<GeneratedRecipeWithMissing> recipes,
        int targetCount,
        int minMissingOneOrTwo)
    {
        if (recipes.Count == 0 || targetCount <= 0)
        {
            return [];
        }

        var missing0 = recipes.Where(r => r.MissingIngredients.Count == 0).ToList();
        var missing12 = recipes.Where(r => r.MissingIngredients.Count is >= 1 and <= 2).ToList();

        var targetMissing12 = Math.Min(minMissingOneOrTwo, missing12.Count);
        var targetMissing0 = Math.Max(0, targetCount - targetMissing12);

        var selected = new List<GeneratedRecipeWithMissing>();
        selected.AddRange(missing0.Take(targetMissing0));
        selected.AddRange(missing12.Take(targetMissing12));

        if (selected.Count < targetCount)
        {
            var remaining = recipes.Where(r => !selected.Contains(r)).ToList();
            selected.AddRange(remaining.Take(targetCount - selected.Count));
        }

        return selected;
    }

    private List<string> MergeMissingIngredients(GeneratedRecipe recipe, List<string> inventoryNames)
    {
        var missingFromAi = recipe.MissingIngredients
            .Select(NormalizeIngredientName)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingFromInventory = ComputeMissingFromInventory(recipe, inventoryNames)
            .Select(NormalizeIngredientName)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return missingFromAi
            .Union(missingFromInventory, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private List<string> ComputeMissingFromInventory(GeneratedRecipe recipe, List<string> inventoryNames)
    {
        var missing = new List<string>();
        foreach (var ingredient in recipe.Ingredients)
        {
            var normalized = NormalizeIngredientName(ingredient.Name);
            if (string.IsNullOrWhiteSpace(normalized))
                continue;

            if (IsBasicIngredient(normalized))
                continue;

            if (!InventoryMatch(normalized, inventoryNames))
            {
                missing.Add(ingredient.Name);
            }
        }

        return missing;
    }

    private static string NormalizeIngredientName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var normalized = name.ToLowerInvariant().Trim();
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, "\\s+", " ");
        normalized = System.Text.RegularExpressions.Regex.Replace(
            normalized,
            "^(fresh|dried|frozen|canned|chopped|minced|sliced|diced|ground)\\s+",
            "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        var commaIndex = normalized.IndexOf(',', StringComparison.Ordinal);
        if (commaIndex >= 0)
        {
            normalized = normalized[..commaIndex];
        }

        return normalized.Trim();
    }

    private static bool IsBasicIngredient(string normalized)
    {
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        // Use whole-word matching to avoid false positives like "salted butter" containing "salt"
        return BasicIngredients.Any(b =>
            normalized.Equals(b, StringComparison.OrdinalIgnoreCase) ||
            System.Text.RegularExpressions.Regex.IsMatch(
                normalized,
                $@"\b{System.Text.RegularExpressions.Regex.Escape(b)}\b",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase));
    }

    private static bool InventoryMatch(string normalizedIngredient, List<string> inventoryNames)
    {
        foreach (var item in inventoryNames)
        {
            if (item.Equals(normalizedIngredient, StringComparison.OrdinalIgnoreCase))
                return true;

            if (item.Contains(normalizedIngredient, StringComparison.OrdinalIgnoreCase) ||
                normalizedIngredient.Contains(item, StringComparison.OrdinalIgnoreCase))
                return true;

            var ingredientWords = normalizedIngredient.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var itemWords = item.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (ingredientWords.Any(w => itemWords.Any(iw => iw.Contains(w, StringComparison.OrdinalIgnoreCase) || w.Contains(iw, StringComparison.OrdinalIgnoreCase))))
                return true;
        }

        return false;
    }

    private record ReferenceRecipeRow(
        Guid Id,
        string Title,
        string? Description,
        List<string> Ingredients,
        List<string> Tags);

    private record GeneratedRecipeWithMissing(
        GeneratedRecipe Recipe,
        List<string> MissingIngredients);
}
