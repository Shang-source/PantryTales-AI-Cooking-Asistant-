using backend.Dtos.Recipes;
using backend.Interfaces;
using backend.Models;

namespace backend.Services;

public class RecipeCookService(
    IRecipeCookRepository recipeCookRepository,
    IRecipeRepository recipeRepository,
    IUserRepository userRepository,
    ILogger<RecipeCookService> logger) : IRecipeCookService
{
    public async Task<RecipeCookResponseDto?> RecordCookAsync(Guid recipeId, string clerkUserId,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByClerkUserIdAsync(clerkUserId, cancellationToken);
        if (user is null)
        {
            logger.LogWarning("Record cook rejected: Clerk user {ClerkUserId} not found.", clerkUserId);
            return null;
        }

        var recipe = await recipeRepository.GetByIdAsync(recipeId, cancellationToken);
        if (recipe is null)
        {
            logger.LogWarning("Record cook rejected: Recipe {RecipeId} not found.", recipeId);
            return null;
        }

        var now = DateTime.UtcNow;

        // Check if entry already exists for this user-recipe pair
        var existingEntry = await recipeCookRepository.GetByUserAndRecipeAsync(user.Id, recipe.Id, cancellationToken);

        if (existingEntry is not null)
        {
            // Increment cook count and update last cooked time
            existingEntry.CookCount++;
            existingEntry.LastCookedAt = now;
            await recipeCookRepository.SaveChangesAsync(cancellationToken);

            logger.LogInformation("User {UserId} completed cooking recipe {RecipeId}. Total cooks: {CookCount}",
                user.Id, recipe.Id, existingEntry.CookCount);

            return new RecipeCookResponseDto(recipe.Id, existingEntry.Id, existingEntry.CookCount, now);
        }

        // Create new entry
        var cookEntry = new RecipeCook
        {
            UserId = user.Id,
            RecipeId = recipe.Id,
            CookCount = 1,
            FirstCookedAt = now,
            LastCookedAt = now
        };

        await recipeCookRepository.AddAsync(cookEntry, cancellationToken);
        await recipeCookRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("User {UserId} completed cooking recipe {RecipeId} for the first time.",
            user.Id, recipe.Id);

        return new RecipeCookResponseDto(recipe.Id, cookEntry.Id, 1, now);
    }

    public async Task<IReadOnlyList<MyCookedRecipeCardDto>?> GetMyCookedRecipesAsync(
        string clerkUserId,
        int page,
        int pageSize,
        string? searchQuery,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByClerkUserIdAsync(clerkUserId, cancellationToken);
        if (user is null)
        {
            logger.LogWarning("Get cooking history rejected: Clerk user {ClerkUserId} not found.", clerkUserId);
            return null;
        }

        var history = await recipeCookRepository.GetUserCookingHistoryAsync(
            user.Id,
            page,
            pageSize,
            searchQuery,
            cancellationToken);

        return history.Select(item =>
        {
            var recipe = item.Recipe;
            var coverImageUrl = recipe.ImageUrls?.FirstOrDefault();
            var authorName = recipe.Author?.Nickname ?? "";

            return new MyCookedRecipeCardDto(
                CookId: item.Id,
                Id: recipe.Id,
                Title: recipe.Title,
                Description: recipe.Description,
                CoverImageUrl: coverImageUrl,
                AuthorId: recipe.AuthorId,
                AuthorName: authorName,
                CookCount: item.CookCount,
                LastCookedAt: item.LastCookedAt,
                FirstCookedAt: item.FirstCookedAt
            );
        }).ToList();
    }

    public async Task<int?> GetMyCooksCountAsync(string clerkUserId, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByClerkUserIdAsync(clerkUserId, cancellationToken);
        if (user is null)
        {
            logger.LogWarning("Get cooking count rejected: Clerk user {ClerkUserId} not found.", clerkUserId);
            return null;
        }

        return await recipeCookRepository.GetUniqueRecipeCountAsync(user.Id, cancellationToken);
    }

    public async Task<bool> DeleteCookEntryAsync(Guid cookId, string clerkUserId,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByClerkUserIdAsync(clerkUserId, cancellationToken);
        if (user is null)
        {
            logger.LogWarning("Delete cook entry rejected: Clerk user {ClerkUserId} not found.", clerkUserId);
            return false;
        }

        var cookEntry = await recipeCookRepository.GetByIdAsync(cookId, cancellationToken);
        if (cookEntry is null)
        {
            logger.LogWarning("Delete cook entry rejected: Cook entry {CookId} not found.", cookId);
            return false;
        }

        // Ensure the user owns this cook entry
        if (cookEntry.UserId != user.Id)
        {
            logger.LogWarning("Delete cook entry rejected: User {UserId} does not own cook entry {CookId}.",
                user.Id, cookId);
            return false;
        }

        await recipeCookRepository.DeleteAsync(cookEntry, cancellationToken);
        await recipeCookRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("User {UserId} deleted cook entry {CookId} for recipe {RecipeId}.",
            user.Id, cookId, cookEntry.RecipeId);

        return true;
    }

    public async Task<bool> ClearAllCookHistoryAsync(string clerkUserId,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByClerkUserIdAsync(clerkUserId, cancellationToken);
        if (user is null)
        {
            logger.LogWarning("Clear cooking history rejected: Clerk user {ClerkUserId} not found.", clerkUserId);
            return false;
        }

        await recipeCookRepository.DeleteAllForUserAsync(user.Id, cancellationToken);

        logger.LogInformation("User {UserId} cleared all cooking history.", user.Id);

        return true;
    }
}
