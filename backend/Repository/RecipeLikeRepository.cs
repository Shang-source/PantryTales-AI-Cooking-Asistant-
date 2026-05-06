using backend.Data;
using backend.Interfaces;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Repository;

public class RecipeLikeRepository(AppDbContext context) : IRecipeLikeRepository
{
    public async Task<RecipeLike?> GetRecipeLikeAsync(Guid userId, Guid recipeId,
        CancellationToken cancellationToken = default)
    {
        return await context.RecipeLikes
            .FindAsync(new object?[] { userId, recipeId }, cancellationToken);
    }

    public async Task AddRecipeLikeAsync(RecipeLike recipeLike, CancellationToken cancellationToken = default)
    {
        await context.RecipeLikes.AddAsync(recipeLike, cancellationToken);
    }

    public Task RemoveRecipeLikeAsync(RecipeLike recipeLike, CancellationToken cancellationToken = default)
    {
        context.RecipeLikes.Remove(recipeLike);
        return Task.CompletedTask;
    }

    public async Task<int> IncrementRecipeLikesCountAsync(Guid recipeId, DateTime updatedAt,
        CancellationToken cancellationToken = default)
    {
        var affected = await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE recipes SET likes_count = likes_count + 1, updated_at = {updatedAt} WHERE id = {recipeId}",
            cancellationToken);

        if (affected == 0)
        {
            return 0;
        }

        return await context.Recipes
            .AsNoTracking()
            .Where(recipe => recipe.Id == recipeId)
            .Select(recipe => recipe.LikesCount)
            .SingleAsync(cancellationToken);
    }

    public async Task<int> DecrementRecipeLikesCountAsync(Guid recipeId, DateTime updatedAt,
        CancellationToken cancellationToken = default)
    {
        var affected = await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE recipes SET likes_count = GREATEST(likes_count - 1, 0), updated_at = {updatedAt} WHERE id = {recipeId}",
            cancellationToken);

        if (affected == 0)
        {
            return 0;
        }

        return await context.Recipes
            .AsNoTracking()
            .Where(recipe => recipe.Id == recipeId)
            .Select(recipe => recipe.LikesCount)
            .SingleAsync(cancellationToken);
    }

    public async Task<int?> GetRecipeLikesCountAsync(Guid recipeId, CancellationToken cancellationToken = default)
    {
        return await context.Recipes
            .AsNoTracking()
            .Where(recipe => recipe.Id == recipeId)
            .Select(recipe => (int?)recipe.LikesCount)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
