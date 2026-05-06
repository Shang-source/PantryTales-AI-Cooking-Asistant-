using System.Security.Claims;
using System.Text.Json;
using backend.Data;
using backend.Dtos.Recipes;
using backend.Extensions;
using backend.Interfaces;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public class RecipeService(
    AppDbContext dbContext,
    IRecipeRepository recipeRepository,
    ILogger<RecipeService> logger) : IRecipeService
{
    private const string RecipeTagType = "recipe";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CreateRecipeResult> CreateAsync(CreateRecipeRequestDto request, string clerkUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clerkUserId))
        {
            logger.LogWarning("Create recipe rejected: Missing Clerk user id.");
            return new CreateRecipeResult(CreateRecipeResultStatus.UserNotFound, null);
        }

        var normalizedTitle = request.Title?.Trim().ToTitleCase();
        var normalizedDescription = request.Description?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedTitle))
        {
            logger.LogWarning("Create recipe rejected: Title missing for Clerk user {ClerkUserId}.",
                clerkUserId);
            return new CreateRecipeResult(CreateRecipeResultStatus.InvalidRequest, null);
        }

        var steps = (request.Steps ?? [])
            .Select(step => step?.Trim())
            .Where(step => !string.IsNullOrWhiteSpace(step))
            .Select(step => step!)
            .ToList();

        if (steps.Count == 0)
        {
            logger.LogWarning("Create recipe rejected: No valid steps provided for Clerk user {ClerkUserId}.",
                clerkUserId);
            return new CreateRecipeResult(CreateRecipeResultStatus.InvalidRequest, null);
        }

        var user = await dbContext.Users
            .Where(u => u.ClerkUserId == clerkUserId)
            .Select(u => new { u.Id })
            .SingleOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            logger.LogWarning("Create recipe rejected: Clerk user {ClerkUserId} not found.", clerkUserId);
            return new CreateRecipeResult(CreateRecipeResultStatus.UserNotFound, null);
        }

        var householdId = await dbContext.HouseholdMembers
            .Where(member => member.UserId == user.Id)
            .OrderBy(member => member.JoinedAt)
            .Select(member => (Guid?)member.HouseholdId)
            .FirstOrDefaultAsync(cancellationToken);

        if (householdId is null)
        {
            logger.LogWarning("Create recipe rejected for user {UserId}: No household membership found.", user.Id);
            return new CreateRecipeResult(CreateRecipeResultStatus.HouseholdNotFound, null);
        }

        var now = DateTime.UtcNow;
        var imageUrls = request.ImageUrls?
            .Select(url => url?.Trim())
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => url!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var recipe = new Recipe
        {
            Id = Guid.CreateVersion7(),
            HouseholdId = householdId.Value,
            AuthorId = user.Id,
            Title = normalizedTitle!,
            Description = normalizedDescription!,
            Steps = JsonSerializer.Serialize(steps, JsonOptions),
            Visibility = request.Visibility,
            ImageUrls = imageUrls is { Count: > 0 } ? imageUrls : null,
            Type = request.Type ?? RecipeType.User,
            LikesCount = 0,
            CommentsCount = 0,
            SavedCount = 0,
            CreatedAt = now,
            UpdatedAt = now,
            Servings = request.Servings,
            TotalTimeMinutes = request.TotalTimeMinutes,
            Difficulty = request.Difficulty ?? RecipeDifficulty.None,
            // Nutrition data from request
            Calories = request.Nutrition?.Calories,
            Carbohydrates = request.Nutrition?.Carbohydrates,
            Fat = request.Nutrition?.Fat,
            Protein = request.Nutrition?.Protein,
            Sugar = request.Nutrition?.Sugar,
            Sodium = request.Nutrition?.Sodium,
            SaturatedFat = request.Nutrition?.SaturatedFat
        };

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        dbContext.Recipes.Add(recipe);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Add ingredients if provided
        if (request.Ingredients is { Count: > 0 })
        {
            // Get all ingredient names for lookup
            var ingredientNames = request.Ingredients
                .Select(i => i.Name.Trim().ToLowerInvariant())
                .Distinct()
                .ToList();

            // Fetch existing ingredients from DB with their tags
            var existingIngredients = await dbContext.Ingredients
                .Include(i => i.Tags)
                .Where(i => ingredientNames.Contains(i.CanonicalName.ToLower()))
                .ToDictionaryAsync(i => i.CanonicalName.ToLowerInvariant(), i => i, cancellationToken);

            // Pre-load ingredient_type tags for category assignment
            var hasCategories = request.Ingredients.Any(i => !string.IsNullOrEmpty(i.Category));
            var categoryTags = hasCategories
                ? await dbContext.Tags
                    .AsNoTracking()
                    .Where(t => t.Type == "ingredient_type" && t.IsActive)
                    .ToDictionaryAsync(t => t.DisplayName.ToLowerInvariant(), cancellationToken)
                : new Dictionary<string, Tag>();

            // Track newly created ingredients in this batch
            var newIngredients = new Dictionary<string, Ingredient>(StringComparer.OrdinalIgnoreCase);

            foreach (var ingredientDto in request.Ingredients)
            {
                var normalizedName = ingredientDto.Name.Trim().ToLowerInvariant();
                Guid ingredientId;
                Ingredient? ingredient = null;

                // Check existing ingredients from DB
                if (existingIngredients.TryGetValue(normalizedName, out var existingIngredient))
                {
                    ingredientId = existingIngredient.Id;
                    ingredient = existingIngredient;
                }
                // Check newly created ingredients in this batch
                else if (newIngredients.TryGetValue(normalizedName, out var batchIngredient))
                {
                    ingredientId = batchIngredient.Id;
                    ingredient = batchIngredient;
                }
                else
                {
                    // Create new ingredient entity
                    var createdIngredient = new Ingredient
                    {
                        CanonicalName = ingredientDto.Name.Trim(),
                        DefaultUnit = ingredientDto.Unit?.Trim()
                    };
                    dbContext.Ingredients.Add(createdIngredient);
                    newIngredients[normalizedName] = createdIngredient;
                    ingredientId = createdIngredient.Id;
                    ingredient = createdIngredient;
                }

                // Assign category tag if provided and tag exists
                if (!string.IsNullOrEmpty(ingredientDto.Category) && ingredient != null)
                {
                    var categoryKey = ingredientDto.Category.ToLowerInvariant();
                    if (categoryTags.TryGetValue(categoryKey, out var categoryTag))
                    {
                        var hasTag = ingredient.Tags?.Any(t => t.TagId == categoryTag.Id) ?? false;
                        if (!hasTag)
                        {
                            var ingredientTag = new IngredientTag
                            {
                                IngredientId = ingredientId,
                                TagId = categoryTag.Id
                            };
                            dbContext.Set<IngredientTag>().Add(ingredientTag);
                        }
                    }
                }

                var recipeIngredient = new RecipeIngredient
                {
                    Id = Guid.CreateVersion7(),
                    RecipeId = recipe.Id,
                    IngredientId = ingredientId,
                    Amount = ingredientDto.Amount ?? 0,
                    Unit = ingredientDto.Unit?.Trim() ?? string.Empty,
                    IsOptional = ingredientDto.IsOptional,
                    CreatedAt = now
                };
                dbContext.RecipeIngredients.Add(recipeIngredient);
            }
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var tagEntities = await ResolveTagsAsync(request.Tags, now, cancellationToken);

        if (tagEntities.Count > 0)
        {
            foreach (var tag in tagEntities)
            {
                dbContext.RecipeTags.Add(new RecipeTag
                {
                    RecipeId = recipe.Id,
                    TagId = tag.Id
                });
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        var createdRecipe = await recipeRepository.GetDetailedByIdAsync(recipe.Id, cancellationToken);
        if (createdRecipe is null)
        {
            logger.LogError("Created recipe {RecipeId} could not be loaded for response.", recipe.Id);
            return new CreateRecipeResult(CreateRecipeResultStatus.Failed, null);
        }

        logger.LogInformation("Created recipe {RecipeId} with {TagCount} tags for user {UserId}.",
            recipe.Id,
            tagEntities.Count,
            user.Id);

        return new CreateRecipeResult(CreateRecipeResultStatus.Success, createdRecipe.ToDetailDto());
    }

    private async Task<List<Tag>> ResolveTagsAsync(List<string>? tags, DateTime now,
        CancellationToken cancellationToken)
    {
        if (tags is null || tags.Count == 0)
        {
            return [];
        }

        var tagInputs = tags
            .Select(raw => raw?.Trim())
            .Where(raw => !string.IsNullOrWhiteSpace(raw))
            .Select(raw => new TagInput(raw!, NormalizeTagName(raw!)))
            .Where(input => !string.IsNullOrWhiteSpace(input.Normalized))
            .GroupBy(input => input.Normalized, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();

        if (tagInputs.Count == 0)
        {
            return [];
        }

        var normalizedNames = tagInputs.Select(input => input.Normalized).ToList();
        var displayNames = tagInputs
            .Select(input => FormatDisplayName(input.Normalized))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var candidateNames = normalizedNames
            .Concat(displayNames)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var candidates = await dbContext.Tags
            .Where(tag => tag.Type == RecipeTagType && tag.IsActive)
            .Where(tag => candidateNames.Contains(tag.Name) || candidateNames.Contains(tag.DisplayName))
            .ToListAsync(cancellationToken);

        var existingTags = new Dictionary<string, Tag>(StringComparer.Ordinal);
        foreach (var tag in candidates)
        {
            var normalized = NormalizeTagName(tag.Name);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                normalized = NormalizeTagName(tag.DisplayName);
            }

            if (!string.IsNullOrWhiteSpace(normalized))
            {
                existingTags[normalized] = tag;
            }
        }

        var resolved = new List<Tag>(tagInputs.Count);
        var hasChanges = false;

        foreach (var input in tagInputs)
        {
            var desiredDisplayName = FormatDisplayName(input.Normalized);

            if (!existingTags.TryGetValue(input.Normalized, out var tagEntity))
            {
                tagEntity = new Tag
                {
                    Name = input.Normalized,
                    DisplayName = desiredDisplayName,
                    Type = RecipeTagType,
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                dbContext.Tags.Add(tagEntity);
                existingTags[input.Normalized] = tagEntity;
                hasChanges = true;
            }
            else if (!string.Equals(tagEntity.DisplayName, desiredDisplayName, StringComparison.Ordinal))
            {
                tagEntity.DisplayName = desiredDisplayName;
                tagEntity.UpdatedAt = now;
                hasChanges = true;
            }

            resolved.Add(tagEntity);
        }

        if (hasChanges)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return resolved;
    }

    private static string NormalizeTagName(string value) => value.Trim().ToLowerInvariant();

    private static string FormatDisplayName(string normalized)
    {
        if (string.IsNullOrEmpty(normalized))
        {
            return string.Empty;
        }

        if (normalized.Length == 1)
        {
            return normalized.ToUpperInvariant();
        }

        return char.ToUpperInvariant(normalized[0]) + normalized[1..];
    }

    private sealed record TagInput(string Raw, string Normalized);

    private Task<User?> FindUserByClerkUserIdAsync(string clerkUserId, CancellationToken cancellationToken)
        => dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(user => user.ClerkUserId == clerkUserId, cancellationToken);

    public async Task<RecipeListResult> ListAsync(string? scope, ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            return new RecipeListResult(RecipeListResultStatus.InvalidScope, null, "Scope parameter is required.");
        }

        var normalizedScope = scope.Trim().ToLowerInvariant();
        var recipes = dbContext.Recipes
            .AsNoTracking()
            .Where(recipe => recipe.Type == RecipeType.User);
        IOrderedQueryable<Recipe> orderedRecipes;
        Guid? currentUserId = null;

        // Scope determines whether we return public community recipes or all recipes by the current user.
        switch (normalizedScope)
        {
            case "community":
                orderedRecipes = recipes
                    .Where(recipe => recipe.Visibility == RecipeVisibility.Public)
                    .OrderByDescending(recipe => recipe.CreatedAt);

                if (user.TryGetClerkUserId(out var communityClerkUserId, out _))
                {
                    currentUserId = await dbContext.Users
                        .AsNoTracking()
                        .Where(appUser => appUser.ClerkUserId == communityClerkUserId)
                        .Select(appUser => (Guid?)appUser.Id)
                        .SingleOrDefaultAsync(cancellationToken);
                }
                break;
            case "me":
                if (!user.TryGetClerkUserId(out var clerkUserId, out _))
                {
                    return new RecipeListResult(
                        RecipeListResultStatus.MissingClerkUserId,
                        null,
                        "Could not resolve Clerk user id.");
                }

                var currentUser = await dbContext.Users
                    .AsNoTracking()
                    .SingleOrDefaultAsync(appUser => appUser.ClerkUserId == clerkUserId, cancellationToken);

                if (currentUser is null)
                {
                    return new RecipeListResult(RecipeListResultStatus.UserNotFound, null, "User not found.");
                }

                currentUserId = currentUser.Id;
                orderedRecipes = recipes
                    .Where(recipe => recipe.AuthorId == currentUser.Id)
                    .OrderByDescending(recipe => recipe.UpdatedAt);
                break;
            case "ai-detected":
                if (!user.TryGetClerkUserId(out var aiClerkUserId, out _))
                {
                    return new RecipeListResult(
                        RecipeListResultStatus.MissingClerkUserId,
                        null,
                        "Could not resolve Clerk user id.");
                }

                var aiUser = await dbContext.Users
                    .AsNoTracking()
                    .SingleOrDefaultAsync(appUser => appUser.ClerkUserId == aiClerkUserId, cancellationToken);

                if (aiUser is null)
                {
                    return new RecipeListResult(RecipeListResultStatus.UserNotFound, null, "User not found.");
                }

                var userHouseholdId = await dbContext.HouseholdMembers
                    .Where(m => m.UserId == aiUser.Id)
                    .Select(m => (Guid?)m.HouseholdId)
                    .FirstOrDefaultAsync(cancellationToken);

                orderedRecipes = dbContext.Recipes
                    .AsNoTracking()
                    .Where(r => r.Type == RecipeType.Model && r.HouseholdId == userHouseholdId)
                    .OrderByDescending(r => r.CreatedAt);
                break;
            default:
                return new RecipeListResult(RecipeListResultStatus.InvalidScope, null, "Unsupported scope value.");
        }

        var authorQuery = dbContext.Users.AsNoTracking();

        var recipeProjections = currentUserId.HasValue
            ? await (from recipe in orderedRecipes
                     let imageUrls = recipe.ImageUrls
                     join author in authorQuery on recipe.AuthorId equals author.Id into authorGroup
                     from author in authorGroup.DefaultIfEmpty()
                     join myLike in dbContext.RecipeLikes.AsNoTracking().Where(rl => rl.UserId == currentUserId.Value)
                         on recipe.Id equals myLike.RecipeId into myLikeGroup
                     from myLike in myLikeGroup.DefaultIfEmpty()
                     join mySave in dbContext.RecipeSaves.AsNoTracking().Where(rs => rs.UserId == currentUserId.Value)
                         on recipe.Id equals mySave.RecipeId into mySaveGroup
                     from mySave in mySaveGroup.DefaultIfEmpty()
                     select new
                     {
                         recipe.Id,
                         recipe.AuthorId,
                         AuthorNickname = author != null ? author.Nickname : string.Empty,
                         AuthorAvatarUrl = author != null ? author.AvatarUrl : null,
                         recipe.Title,
                         recipe.Description,
                         // Only the first image is considered the cover.
                         CoverImageUrl = imageUrls != null && imageUrls.Count > 0 ? imageUrls[0] : null,
                         recipe.Visibility,
                         recipe.Type,
                         recipe.LikesCount,
                         LikedByMe = myLike != null,
                         recipe.CommentsCount,
                         recipe.SavedCount,
                         SavedByMe = mySave != null,
                         recipe.CreatedAt,
                         recipe.UpdatedAt
                     }).ToListAsync(cancellationToken)
            : await (from recipe in orderedRecipes
                     let imageUrls = recipe.ImageUrls
                     join author in authorQuery on recipe.AuthorId equals author.Id into authorGroup
                     from author in authorGroup.DefaultIfEmpty()
                     select new
                     {
                         recipe.Id,
                         recipe.AuthorId,
                         AuthorNickname = author != null ? author.Nickname : string.Empty,
                         AuthorAvatarUrl = author != null ? author.AvatarUrl : null,
                         recipe.Title,
                         recipe.Description,
                         // Only the first image is considered the cover.
                         CoverImageUrl = imageUrls != null && imageUrls.Count > 0 ? imageUrls[0] : null,
                         recipe.Visibility,
                         recipe.Type,
                         recipe.LikesCount,
                         LikedByMe = false,
                         recipe.CommentsCount,
                         recipe.SavedCount,
                         SavedByMe = false,
                         recipe.CreatedAt,
                         recipe.UpdatedAt
                     }).ToListAsync(cancellationToken);

        var recipeIds = recipeProjections.Select(recipe => recipe.Id).ToList();
        var tagsLookup = new Dictionary<Guid, List<string>>();

        if (recipeIds.Count > 0)
        {
            var tagEntries = await dbContext.RecipeTags
                .AsNoTracking()
                .Where(tag => recipeIds.Contains(tag.RecipeId) && tag.Tag.Type == RecipeTagType && tag.Tag.IsActive)
                .Select(tag => new
                {
                    tag.RecipeId,
                    // DisplayName contains the formatted, user-facing label used for recipes.
                    TagName = tag.Tag.DisplayName
                })
                 .ToListAsync(cancellationToken);

            foreach (var group in tagEntries.GroupBy(entry => entry.RecipeId))
            {
                var orderedTags = group
                    .Select(entry => entry.TagName)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToList();

                tagsLookup[group.Key] = orderedTags;
            }
        }

        var recipeCards = recipeProjections
            .Select(recipe => new RecipeCardDto(
                recipe.Id,
                recipe.AuthorId,
                recipe.AuthorNickname,
                recipe.AuthorAvatarUrl,
                recipe.Title.ToTitleCase(),
                recipe.Description ?? string.Empty,
                recipe.CoverImageUrl,
                recipe.Visibility,
                recipe.Type,
                recipe.LikesCount,
                recipe.LikedByMe,
                recipe.CommentsCount,
                recipe.SavedCount,
                recipe.SavedByMe,
                recipe.CreatedAt,
                recipe.UpdatedAt,
                tagsLookup.TryGetValue(recipe.Id, out var tags) ? tags : Array.Empty<string>()))
            .ToList();

        return new RecipeListResult(RecipeListResultStatus.Success, recipeCards);
    }

    public async Task<RecipeDetailResult> GetByIdAsync(Guid recipeId, ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var recipe = await recipeRepository.GetDetailedByIdAsync(recipeId, cancellationToken);

        // Allow User, Model, and System type recipes
        if (recipe is null)
        {
            logger.LogWarning("Recipe detail {RecipeId} not found.", recipeId);
            return new RecipeDetailResult(RecipeDetailResultStatus.RecipeNotFound, null, "Recipe not found.");
        }

        Guid? currentUserId = null;
        if (user.TryGetClerkUserId(out var optionalClerkUserId, out _))
        {
            currentUserId = await dbContext.Users
                .AsNoTracking()
                .Where(appUser => appUser.ClerkUserId == optionalClerkUserId)
                .Select(appUser => (Guid?)appUser.Id)
                .SingleOrDefaultAsync(cancellationToken);
        }

        if (recipe.Visibility == RecipeVisibility.Private)
        {
            if (!user.TryGetClerkUserId(out var clerkUserId, out var failureReason))
            {
                logger.LogWarning("Recipe detail {RecipeId} rejected: {Reason}", recipeId,
                    failureReason ?? "Missing Clerk user id.");
                return new RecipeDetailResult(
                    RecipeDetailResultStatus.MissingClerkUserId,
                    null,
                    "Could not determine Clerk user id.");
            }

            if (!currentUserId.HasValue)
            {
                logger.LogWarning("Recipe detail {RecipeId} rejected: Clerk user {ClerkUserId} not found.",
                    recipeId,
                    clerkUserId);
                return new RecipeDetailResult(RecipeDetailResultStatus.UserNotFound, null, "User not found.");
            }

            // For Model (AI-generated) recipes, allow access if user is in the same household
            if (recipe.Type == RecipeType.Model && recipe.HouseholdId != Guid.Empty)
            {
                var isHouseholdMember = await dbContext.HouseholdMembers
                    .AsNoTracking()
                    .AnyAsync(hm => hm.HouseholdId == recipe.HouseholdId && hm.UserId == currentUserId.Value, cancellationToken);

                if (!isHouseholdMember)
                {
                    logger.LogWarning("Recipe detail {RecipeId} rejected: User {UserId} is not a member of household {HouseholdId}.",
                        recipeId, currentUserId.Value, recipe.HouseholdId);
                    return new RecipeDetailResult(
                        RecipeDetailResultStatus.Unauthorized,
                        null,
                        "Not authorized to view this recipe.");
                }
            }
            else if (!recipe.AuthorId.HasValue || recipe.AuthorId.Value != currentUserId.Value)
            {
                logger.LogWarning("Recipe detail {RecipeId} rejected: User {UserId} is not the author.",
                    recipeId,
                    currentUserId.Value);
                return new RecipeDetailResult(
                    RecipeDetailResultStatus.Unauthorized,
                    null,
                    "Not authorized to view this recipe.");
            }
        }

        var likedByMe = false;
        var savedByMe = false;
        if (currentUserId.HasValue)
        {
            likedByMe = await dbContext.RecipeLikes
                .AsNoTracking()
                .AnyAsync(like => like.UserId == currentUserId.Value && like.RecipeId == recipeId, cancellationToken);

            savedByMe = await dbContext.RecipeSaves
                .AsNoTracking()
                .AnyAsync(save => save.UserId == currentUserId.Value && save.RecipeId == recipeId, cancellationToken);
        }

        return new RecipeDetailResult(RecipeDetailResultStatus.Success, recipe.ToDetailDto(likedByMe, savedByMe));
    }

    public async Task<DeleteRecipeResult> DeleteAsync(Guid recipeId, string clerkUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clerkUserId))
        {
            return new DeleteRecipeResult(DeleteRecipeResultStatus.UserNotFound, "Clerk user id is missing.");
        }

        var user = await FindUserByClerkUserIdAsync(clerkUserId, cancellationToken);
        if (user is null)
        {
            return new DeleteRecipeResult(DeleteRecipeResultStatus.UserNotFound, "User not found.");
        }

        var recipe = await dbContext.Recipes
            .FirstOrDefaultAsync(r => r.Id == recipeId, cancellationToken);

        if (recipe is null || recipe.Type != RecipeType.User)
        {
            return new DeleteRecipeResult(DeleteRecipeResultStatus.RecipeNotFound, "Recipe not found.");
        }

        if (recipe.AuthorId != user.Id)
        {
            return new DeleteRecipeResult(DeleteRecipeResultStatus.Unauthorized, "Not authorized to delete this recipe.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var tags = await dbContext.RecipeTags
            .Where(rt => rt.RecipeId == recipeId)
            .ToListAsync(cancellationToken);
        dbContext.RecipeTags.RemoveRange(tags);

        dbContext.Recipes.Remove(recipe);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new DeleteRecipeResult(DeleteRecipeResultStatus.Success);
    }

    public async Task<UpdateRecipeResult> UpdateAsync(
        Guid recipeId,
        CreateRecipeRequestDto request,
        string clerkUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clerkUserId))
        {
            return new UpdateRecipeResult(UpdateRecipeResultStatus.UserNotFound, null, "Clerk user id is missing.");
        }

        var normalizedTitle = request.Title?.Trim().ToTitleCase();
        var normalizedDescription = request.Description?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedTitle))
        {
            return new UpdateRecipeResult(UpdateRecipeResultStatus.InvalidRequest, null, "Title is required.");
        }

        var steps = (request.Steps ?? [])
            .Select(step => step?.Trim())
            .Where(step => !string.IsNullOrWhiteSpace(step))
            .Select(step => step!)
            .ToList();

        if (steps.Count == 0)
        {
            return new UpdateRecipeResult(UpdateRecipeResultStatus.InvalidRequest, null, "At least one step is required.");
        }

        var user = await FindUserByClerkUserIdAsync(clerkUserId, cancellationToken);
        if (user is null)
        {
            return new UpdateRecipeResult(UpdateRecipeResultStatus.UserNotFound, null, "User not found.");
        }

        var recipe = await dbContext.Recipes
            .Include(r => r.Tags)
            .FirstOrDefaultAsync(r => r.Id == recipeId, cancellationToken);

        if (recipe is null || recipe.Type != RecipeType.User)
        {
            return new UpdateRecipeResult(UpdateRecipeResultStatus.RecipeNotFound, null, "Recipe not found.");
        }

        if (!recipe.AuthorId.HasValue || recipe.AuthorId.Value != user.Id)
        {
            return new UpdateRecipeResult(UpdateRecipeResultStatus.Unauthorized, null, "Not authorized to update this recipe.");
        }

        var imageUrls = request.ImageUrls?
            .Select(url => url?.Trim())
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => url!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var now = DateTime.UtcNow;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        recipe.Title = normalizedTitle!;
        recipe.Description = normalizedDescription!;
        recipe.Steps = JsonSerializer.Serialize(steps, JsonOptions);
        recipe.Visibility = request.Visibility;
        recipe.ImageUrls = imageUrls is { Count: > 0 } ? imageUrls : null;
        recipe.Servings = request.Servings;
        recipe.TotalTimeMinutes = request.TotalTimeMinutes;
        recipe.Difficulty = request.Difficulty ?? RecipeDifficulty.None;
        recipe.UpdatedAt = now;

        // Update ingredients: remove existing and add new ones
        var existingRecipeIngredients = await dbContext.RecipeIngredients
            .Where(ri => ri.RecipeId == recipeId)
            .ToListAsync(cancellationToken);
        dbContext.RecipeIngredients.RemoveRange(existingRecipeIngredients);

        if (request.Ingredients is { Count: > 0 })
        {
            // Get all ingredient names for lookup
            var ingredientNames = request.Ingredients
                .Select(i => i.Name.Trim().ToLowerInvariant())
                .Distinct()
                .ToList();

            // Fetch existing ingredients from DB with their tags
            var existingIngredients = await dbContext.Ingredients
                .Include(i => i.Tags)
                .Where(i => ingredientNames.Contains(i.CanonicalName.ToLower()))
                .ToDictionaryAsync(i => i.CanonicalName.ToLowerInvariant(), i => i, cancellationToken);

            // Pre-load ingredient_type tags for category assignment
            var hasCategories = request.Ingredients.Any(i => !string.IsNullOrEmpty(i.Category));
            var categoryTags = hasCategories
                ? await dbContext.Tags
                    .AsNoTracking()
                    .Where(t => t.Type == "ingredient_type" && t.IsActive)
                    .ToDictionaryAsync(t => t.DisplayName.ToLowerInvariant(), cancellationToken)
                : new Dictionary<string, Tag>();

            // Track newly created ingredients in this batch
            var newIngredients = new Dictionary<string, Ingredient>(StringComparer.OrdinalIgnoreCase);

            foreach (var ingredientDto in request.Ingredients)
            {
                var normalizedName = ingredientDto.Name.Trim().ToLowerInvariant();
                Guid ingredientId;
                Ingredient? ingredient = null;

                // Check existing ingredients from DB
                if (existingIngredients.TryGetValue(normalizedName, out var existingIngredient))
                {
                    ingredientId = existingIngredient.Id;
                    ingredient = existingIngredient;
                }
                // Check newly created ingredients in this batch
                else if (newIngredients.TryGetValue(normalizedName, out var batchIngredient))
                {
                    ingredientId = batchIngredient.Id;
                    ingredient = batchIngredient;
                }
                else
                {
                    // Create new ingredient entity
                    var createdIngredient = new Ingredient
                    {
                        CanonicalName = ingredientDto.Name.Trim(),
                        DefaultUnit = ingredientDto.Unit?.Trim()
                    };
                    dbContext.Ingredients.Add(createdIngredient);
                    newIngredients[normalizedName] = createdIngredient;
                    ingredientId = createdIngredient.Id;
                    ingredient = createdIngredient;
                }

                // Assign category tag if provided and tag exists
                if (!string.IsNullOrEmpty(ingredientDto.Category) && ingredient != null)
                {
                    var categoryKey = ingredientDto.Category.ToLowerInvariant();
                    if (categoryTags.TryGetValue(categoryKey, out var categoryTag))
                    {
                        var hasTag = ingredient.Tags?.Any(t => t.TagId == categoryTag.Id) ?? false;
                        if (!hasTag)
                        {
                            var ingredientTag = new IngredientTag
                            {
                                IngredientId = ingredientId,
                                TagId = categoryTag.Id
                            };
                            dbContext.Set<IngredientTag>().Add(ingredientTag);
                        }
                    }
                }

                var recipeIngredient = new RecipeIngredient
                {
                    Id = Guid.CreateVersion7(),
                    RecipeId = recipe.Id,
                    IngredientId = ingredientId,
                    Amount = ingredientDto.Amount ?? 0,
                    Unit = ingredientDto.Unit?.Trim() ?? string.Empty,
                    IsOptional = ingredientDto.IsOptional,
                    CreatedAt = now
                };
                dbContext.RecipeIngredients.Add(recipeIngredient);
            }
        }

        var existingRecipeTags = await dbContext.RecipeTags
            .Where(rt => rt.RecipeId == recipeId)
            .ToListAsync(cancellationToken);
        dbContext.RecipeTags.RemoveRange(existingRecipeTags);

        var tagEntities = await ResolveTagsAsync(request.Tags, now, cancellationToken);
        foreach (var tag in tagEntities)
        {
            dbContext.RecipeTags.Add(new RecipeTag
            {
                RecipeId = recipe.Id,
                TagId = tag.Id
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var updatedRecipe = await recipeRepository.GetDetailedByIdAsync(recipe.Id, cancellationToken);
        if (updatedRecipe is null)
        {
            logger.LogError("Updated recipe {RecipeId} could not be loaded for response.", recipe.Id);
            return new UpdateRecipeResult(UpdateRecipeResultStatus.Failed, null, "Failed to load updated recipe.");
        }

        return new UpdateRecipeResult(UpdateRecipeResultStatus.Success, updatedRecipe.ToDetailDto());
    }
}
