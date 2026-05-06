using backend.Data;
using backend.Interfaces;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Repository;

public class RecipeSaveRepository(AppDbContext context) : IRecipeSaveRepository
{
    public async Task<RecipeSave?> GetRecipeSaveAsync(Guid userId, Guid recipeId,
        CancellationToken cancellationToken = default)
    {
        return await context.RecipeSaves
            .FindAsync(new object?[] { userId, recipeId }, cancellationToken);
    }

    public async Task AddRecipeSaveAsync(RecipeSave recipeSave, CancellationToken cancellationToken = default)
    {
        await context.RecipeSaves.AddAsync(recipeSave, cancellationToken);
    }

    public Task RemoveRecipeSaveAsync(RecipeSave recipeSave, CancellationToken cancellationToken = default)
    {
        context.RecipeSaves.Remove(recipeSave);
        return Task.CompletedTask;
    }

    public async Task<int> IncrementRecipeSavedCountAsync(Guid recipeId, DateTime updatedAt,
        CancellationToken cancellationToken = default)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE recipes SET saved_count = saved_count + 1, updated_at = {updatedAt} WHERE id = {recipeId}",
            cancellationToken);

        return await context.Recipes
            .AsNoTracking()
            .Where(recipe => recipe.Id == recipeId)
            .Select(recipe => recipe.SavedCount)
            .SingleAsync(cancellationToken);
    }

    public async Task<int> DecrementRecipeSavedCountAsync(Guid recipeId, DateTime updatedAt,
        CancellationToken cancellationToken = default)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE recipes SET saved_count = GREATEST(saved_count - 1, 0), updated_at = {updatedAt} WHERE id = {recipeId}",
            cancellationToken);

        return await context.Recipes
            .AsNoTracking()
            .Where(recipe => recipe.Id == recipeId)
            .Select(recipe => recipe.SavedCount)
            .SingleAsync(cancellationToken);
    }

    public async Task<int?> GetRecipeSavedCountAsync(Guid recipeId, CancellationToken cancellationToken = default)
    {
        return await context.Recipes
            .AsNoTracking()
            .Where(recipe => recipe.Id == recipeId)
            .Select(recipe => (int?)recipe.SavedCount)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}

