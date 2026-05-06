using backend.Data;
using backend.Dtos.Recipes;
using backend.Interfaces;
using backend.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace backend.Services;

public class RecipeSaveService(
    AppDbContext dbContext,
    IRecipeSaveRepository recipeSaveRepository,
    IRecipeRepository recipeRepository,
    IUserRepository userRepository,
    ILogger<RecipeSaveService> logger) : IRecipeSaveService
{
    public async Task<RecipeSaveResponseDto?> ToggleSaveAsync(Guid recipeId, string clerkUserId,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByClerkUserIdAsync(clerkUserId, cancellationToken);
        if (user is null)
        {
            logger.LogWarning("Toggle save rejected: Clerk user {ClerkUserId} not found in local users table.",
                clerkUserId);
            return null;
        }

        var recipe = await recipeRepository.GetByIdAsync(recipeId, cancellationToken);
        if (recipe is null)
        {
            logger.LogWarning("Toggle save rejected: Recipe {RecipeId} not found.", recipeId);
            return null;
        }

        var now = DateTime.UtcNow;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var existingSave =
                await recipeSaveRepository.GetRecipeSaveAsync(user.Id, recipe.Id, cancellationToken);

            bool isSaved;
            int savesCount;

            if (existingSave is null)
            {
                var newSave = new RecipeSave
                {
                    UserId = user.Id,
                    RecipeId = recipe.Id,
                    CreatedAt = now
                };

                await recipeSaveRepository.AddRecipeSaveAsync(newSave, cancellationToken);
                recipe.UpdatedAt = now;

                try
                {
                    await recipeSaveRepository.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException ex) when (IsUniqueViolation(ex))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    dbContext.ChangeTracker.Clear();
                    var currentCount = await recipeSaveRepository.GetRecipeSavedCountAsync(recipe.Id, cancellationToken);
                    if (!currentCount.HasValue)
                    {
                        return null;
                    }

                    return new RecipeSaveResponseDto(recipe.Id, true, currentCount.Value);
                }

                savesCount = await recipeSaveRepository.IncrementRecipeSavedCountAsync(recipe.Id, now, cancellationToken);
                isSaved = true;

                logger.LogInformation("User {UserId} saved recipe {RecipeId}.", user.Id, recipe.Id);
            }
            else
            {
                await recipeSaveRepository.RemoveRecipeSaveAsync(existingSave, cancellationToken);
                recipe.UpdatedAt = now;

                try
                {
                    await recipeSaveRepository.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateConcurrencyException)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    dbContext.ChangeTracker.Clear();
                    var currentCount = await recipeSaveRepository.GetRecipeSavedCountAsync(recipe.Id, cancellationToken);
                    if (!currentCount.HasValue)
                    {
                        return null;
                    }

                    return new RecipeSaveResponseDto(recipe.Id, false, currentCount.Value);
                }

                savesCount = await recipeSaveRepository.DecrementRecipeSavedCountAsync(recipe.Id, now, cancellationToken);
                isSaved = false;

                logger.LogInformation("User {UserId} unsaved recipe {RecipeId}.", user.Id, recipe.Id);
            }

            await transaction.CommitAsync(cancellationToken);
            return new RecipeSaveResponseDto(recipe.Id, isSaved, savesCount);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception)
        => exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    public async Task<IReadOnlyList<MySavedRecipeCardDto>?> GetMySavedRecipesAsync(
        Guid currentUserId,
        int page,
        int pageSize,
        SavesCategory category,
        CancellationToken cancellationToken = default)
    {
        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var skip = (safePage - 1) * safePageSize;

        var query = from save in dbContext.RecipeSaves.AsNoTracking()
                    where save.UserId == currentUserId
                    join recipe in dbContext.Recipes.AsNoTracking()
                        on save.RecipeId equals recipe.Id
                    join author in dbContext.Users.AsNoTracking()
                        on recipe.AuthorId equals author.Id into authorGroup
                    from author in authorGroup.DefaultIfEmpty()
                    select new { save, recipe, author };

        // Apply category-specific filter
        query = category switch
        {
            SavesCategory.Recommended => query.Where(x => x.recipe.Type == RecipeType.System),
            SavesCategory.Community => query.Where(x =>
                x.recipe.Type == RecipeType.User && x.recipe.Visibility == RecipeVisibility.Public),
            SavesCategory.Generated => query.Where(x => x.recipe.Type == RecipeType.Model),
            _ => query.Where(x =>
                ((x.recipe.Type == RecipeType.User || x.recipe.Type == RecipeType.System) &&
                 x.recipe.Visibility == RecipeVisibility.Public) ||
                x.recipe.Type == RecipeType.Model)
        };

        var savedRecipes = await query
            .OrderByDescending(x => x.save.CreatedAt)
            .Skip(skip)
            .Take(safePageSize)
            .Select(x => new MySavedRecipeCardDto(
                x.recipe.Id,
                x.recipe.Title,
                x.recipe.Description,
                x.recipe.ImageUrls != null && x.recipe.ImageUrls.Count > 0 ? x.recipe.ImageUrls[0] : null,
                x.recipe.AuthorId,
                x.author != null ? x.author.Nickname : string.Empty,
                x.recipe.SavedCount,
                true,
                x.save.CreatedAt,
                x.recipe.Type))
            .ToListAsync(cancellationToken);

        return savedRecipes;
    }

    public async Task<IReadOnlyList<MySavedRecipeCardDto>?> GetMySavedRecipesAsync(
        Guid currentUserId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        return await GetMySavedRecipesAsync(currentUserId, page, pageSize, SavesCategory.All, cancellationToken);
    }

    public async Task<IReadOnlyList<MySavedRecipeCardDto>?> GetMySavedRecipesAsync(
        string clerkUserId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        return await GetMySavedRecipesAsync(clerkUserId, page, pageSize, SavesCategory.All, cancellationToken);
    }

    public async Task<IReadOnlyList<MySavedRecipeCardDto>?> GetMySavedRecipesAsync(
        string clerkUserId,
        int page,
        int pageSize,
        SavesCategory category,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByClerkUserIdAsync(clerkUserId, cancellationToken);
        if (user is null)
        {
            logger.LogWarning("Get my saved recipes rejected: Clerk user {ClerkUserId} not found.", clerkUserId);
            return null;
        }

        return await GetMySavedRecipesAsync(user.Id, page, pageSize, category, cancellationToken);
    }

    public async Task<int?> GetMySavesCountAsync(string clerkUserId, CancellationToken cancellationToken = default)
    {
        return await GetMySavesCountAsync(clerkUserId, SavesCategory.All, cancellationToken);
    }

    public async Task<int?> GetMySavesCountAsync(string clerkUserId, SavesCategory category,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByClerkUserIdAsync(clerkUserId, cancellationToken);
        if (user is null)
        {
            logger.LogWarning("Get my saves count rejected: Clerk user {ClerkUserId} not found.", clerkUserId);
            return null;
        }

        var query = from save in dbContext.RecipeSaves.AsNoTracking()
                    where save.UserId == user.Id
                    join recipe in dbContext.Recipes.AsNoTracking()
                        on save.RecipeId equals recipe.Id
                    select new { save, recipe };

        query = category switch
        {
            SavesCategory.Recommended => query.Where(x => x.recipe.Type == RecipeType.System),
            SavesCategory.Community => query.Where(x =>
                x.recipe.Type == RecipeType.User && x.recipe.Visibility == RecipeVisibility.Public),
            SavesCategory.Generated => query.Where(x => x.recipe.Type == RecipeType.Model),
            _ => query.Where(x =>
                ((x.recipe.Type == RecipeType.User || x.recipe.Type == RecipeType.System) &&
                 x.recipe.Visibility == RecipeVisibility.Public) ||
                x.recipe.Type == RecipeType.Model)
        };

        return await query
            .Select(x => x.save.RecipeId)
            .Distinct()
            .CountAsync(cancellationToken);
    }
}
