using backend.Data;
using backend.Interfaces;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Repository;

public class RecipeCookRepository(AppDbContext context) : IRecipeCookRepository
{
    public async Task<RecipeCook?> GetByUserAndRecipeAsync(Guid userId, Guid recipeId, CancellationToken cancellationToken = default)
    {
        return await context.RecipeCooks
            .FirstOrDefaultAsync(rc => rc.UserId == userId && rc.RecipeId == recipeId, cancellationToken);
    }

    public async Task AddAsync(RecipeCook recipeCook, CancellationToken cancellationToken = default)
    {
        await context.RecipeCooks.AddAsync(recipeCook, cancellationToken);
    }

    public async Task<RecipeCook?> GetByIdAsync(Guid cookId, CancellationToken cancellationToken = default)
    {
        return await context.RecipeCooks
            .Include(rc => rc.Recipe)
            .FirstOrDefaultAsync(rc => rc.Id == cookId, cancellationToken);
    }

    public async Task<IReadOnlyList<RecipeCook>> GetUserCookingHistoryAsync(
        Guid userId,
        int page,
        int pageSize,
        string? searchQuery,
        CancellationToken cancellationToken = default)
    {
        var query = context.RecipeCooks
            .Where(rc => rc.UserId == userId)
            .Include(rc => rc.Recipe)
                .ThenInclude(r => r.Author)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            var normalized = searchQuery.Trim().ToLower();
            query = query.Where(rc =>
                rc.Recipe.Title.ToLower().Contains(normalized) ||
                (rc.Recipe.Author != null &&
                 rc.Recipe.Author.Nickname.ToLower().Contains(normalized)));
        }

        return await query
            .OrderByDescending(rc => rc.CookCount)
            .ThenByDescending(rc => rc.LastCookedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetUniqueRecipeCountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.RecipeCooks
            .Where(rc => rc.UserId == userId)
            .CountAsync(cancellationToken);
    }

    public Task DeleteAsync(RecipeCook recipeCook, CancellationToken cancellationToken = default)
    {
        context.RecipeCooks.Remove(recipeCook);
        return Task.CompletedTask;
    }

    public async Task DeleteAllForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await context.RecipeCooks
            .Where(rc => rc.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
