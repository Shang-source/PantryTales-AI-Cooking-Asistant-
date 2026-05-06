using backend.Dtos.Recipes;
using backend.Data;
using backend.Interfaces;
using backend.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace backend.Services;

public class RecipeLikeService(
    AppDbContext dbContext,
    IRecipeLikeRepository recipeLikeRepository,
    IRecipeRepository recipeRepository,
    IUserRepository userRepository,
    ILogger<RecipeLikeService> logger) : IRecipeLikeService
{
    public async Task<RecipeLikeResponseDto?> ToggleLikeAsync(Guid recipeId, string clerkUserId,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByClerkUserIdAsync(clerkUserId, cancellationToken);
        if (user is null)
        {
            logger.LogWarning("Toggle like rejected: Clerk user {ClerkUserId} not found in local users table.",
                clerkUserId);
            return null;
        }

        var recipe = await recipeRepository.GetByIdAsync(recipeId, cancellationToken);
        if (recipe is null)
        {
            logger.LogWarning("Toggle like rejected: Recipe {RecipeId} not found.", recipeId);
            return null;
        }

        var now = DateTime.UtcNow;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var existingLike =
                await recipeLikeRepository.GetRecipeLikeAsync(user.Id, recipe.Id, cancellationToken);

            bool isLiked;
            int likesCount;

            if (existingLike is null)
            {
                var newLike = new RecipeLike
                {
                    UserId = user.Id,
                    RecipeId = recipe.Id,
                    CreatedAt = now
                };

                await recipeLikeRepository.AddRecipeLikeAsync(newLike, cancellationToken);
                recipe.UpdatedAt = now;

                try
                {
                    await recipeLikeRepository.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException ex) when (IsUniqueViolation(ex))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    dbContext.ChangeTracker.Clear();
                    var currentCount = await recipeLikeRepository.GetRecipeLikesCountAsync(recipe.Id, cancellationToken);
                    if (!currentCount.HasValue)
                    {
                        return null;
                    }

                    var currentLike = await recipeLikeRepository.GetRecipeLikeAsync(user.Id, recipe.Id, cancellationToken);
                    var isCurrentlyLiked = currentLike is not null;
                    return new RecipeLikeResponseDto(recipe.Id, isCurrentlyLiked, currentCount.Value);
                }

                likesCount = await recipeLikeRepository.IncrementRecipeLikesCountAsync(recipe.Id, now, cancellationToken);
                isLiked = true;

                logger.LogInformation("User {UserId} liked recipe {RecipeId}.", user.Id, recipe.Id);
            }
            else
            {
                await recipeLikeRepository.RemoveRecipeLikeAsync(existingLike, cancellationToken);
                recipe.UpdatedAt = now;

                try
                {
                    await recipeLikeRepository.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateConcurrencyException)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    dbContext.ChangeTracker.Clear();
                    var currentCount = await recipeLikeRepository.GetRecipeLikesCountAsync(recipe.Id, cancellationToken);
                    if (!currentCount.HasValue)
                    {
                        return null;
                    }

                    var currentLike = await recipeLikeRepository.GetRecipeLikeAsync(user.Id, recipe.Id, cancellationToken);
                    var isCurrentlyLiked = currentLike is not null;
                    return new RecipeLikeResponseDto(recipe.Id, isCurrentlyLiked, currentCount.Value);
                }

                likesCount = await recipeLikeRepository.DecrementRecipeLikesCountAsync(recipe.Id, now, cancellationToken);
                isLiked = false;

                logger.LogInformation("User {UserId} unliked recipe {RecipeId}.", user.Id, recipe.Id);
            }

            await transaction.CommitAsync(cancellationToken);
            return new RecipeLikeResponseDto(recipe.Id, isLiked, likesCount);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception)
        => exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    public async Task<IReadOnlyList<MyLikedRecipeCardDto>?> GetMyLikedRecipesAsync(
        Guid currentUserId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // Note: We only return public recipes. Private recipes should not be visible in likes lists,
        // even if the current user is the author.
        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var skip = (safePage - 1) * safePageSize;

        var likedRecipes = await (from like in dbContext.RecipeLikes.AsNoTracking()
                                  where like.UserId == currentUserId
                                  join recipe in dbContext.Recipes.AsNoTracking()
                                      on like.RecipeId equals recipe.Id
                                  where recipe.Type == RecipeType.User &&
                                        recipe.Visibility == RecipeVisibility.Public
                                  join author in dbContext.Users.AsNoTracking()
                                      on recipe.AuthorId equals author.Id into authorGroup
                                  from author in authorGroup.DefaultIfEmpty()
                                  orderby like.CreatedAt descending
                                  select new MyLikedRecipeCardDto(
                                      recipe.Id,
                                      recipe.Title,
                                      recipe.Description,
                                      recipe.ImageUrls != null && recipe.ImageUrls.Count > 0 ? recipe.ImageUrls[0] : null,
                                      recipe.AuthorId,
                                      author != null ? author.Nickname : string.Empty,
                                      recipe.LikesCount,
                                      true,
                                      like.CreatedAt))
            .Skip(skip)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);

        return likedRecipes;
    }

    public async Task<IReadOnlyList<MyLikedRecipeCardDto>?> GetMyLikedRecipesAsync(string clerkUserId, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByClerkUserIdAsync(clerkUserId, cancellationToken);
        if (user is null)
        {
            logger.LogWarning("Get my liked recipes rejected: Clerk user {ClerkUserId} not found.", clerkUserId);
            return null;
        }

        return await GetMyLikedRecipesAsync(user.Id, page, pageSize, cancellationToken);
    }

    public async Task<int?> GetMyLikesCountAsync(string clerkUserId, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByClerkUserIdAsync(clerkUserId, cancellationToken);
        if (user is null)
        {
            logger.LogWarning("Get my likes count rejected: Clerk user {ClerkUserId} not found.", clerkUserId);
            return null;
        }

        return await (from like in dbContext.RecipeLikes.AsNoTracking()
                      where like.UserId == user.Id
                      join recipe in dbContext.Recipes.AsNoTracking()
                          on like.RecipeId equals recipe.Id
                      where recipe.Type == RecipeType.User &&
                            recipe.Visibility == RecipeVisibility.Public
                      select like.RecipeId)
            .Distinct()
            .CountAsync(cancellationToken);
    }
}
