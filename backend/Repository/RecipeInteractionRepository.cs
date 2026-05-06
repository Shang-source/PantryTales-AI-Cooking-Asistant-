using backend.Data;
using backend.Interfaces;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Repository;

public class RecipeInteractionRepository(AppDbContext context) : IRecipeInteractionRepository
{
    public async Task AddAsync(RecipeInteraction interaction, CancellationToken cancellationToken = default)
    {
        await context.RecipeInteractions.AddAsync(interaction, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<RecipeInteraction> interactions, CancellationToken cancellationToken = default)
    {
        await context.RecipeInteractions.AddRangeAsync(interactions, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<RecipeInteraction>> GetByUserIdAsync(
        Guid userId,
        RecipeInteractionEventType? eventType = null,
        DateTime? since = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var query = context.RecipeInteractions
            .Where(ri => ri.UserId == userId);

        if (eventType.HasValue)
            query = query.Where(ri => ri.EventType == eventType.Value);

        if (since.HasValue)
            query = query.Where(ri => ri.CreatedAt >= since.Value);

        query = query.OrderByDescending(ri => ri.CreatedAt);

        if (limit.HasValue)
            query = query.Take(limit.Value);

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<List<RecipeInteraction>> GetByRecipeIdAsync(
        Guid recipeId,
        RecipeInteractionEventType? eventType = null,
        DateTime? since = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var query = context.RecipeInteractions
            .Where(ri => ri.RecipeId == recipeId);

        if (eventType.HasValue)
            query = query.Where(ri => ri.EventType == eventType.Value);

        if (since.HasValue)
            query = query.Where(ri => ri.CreatedAt >= since.Value);

        query = query.OrderByDescending(ri => ri.CreatedAt);

        if (limit.HasValue)
            query = query.Take(limit.Value);

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<Dictionary<RecipeInteractionEventType, int>> GetEventCountsForRecipeAsync(
        Guid recipeId,
        DateTime? since = null,
        CancellationToken cancellationToken = default)
    {
        var query = context.RecipeInteractions
            .Where(ri => ri.RecipeId == recipeId);

        if (since.HasValue)
            query = query.Where(ri => ri.CreatedAt >= since.Value);

        return await query
            .GroupBy(ri => ri.EventType)
            .Select(g => new { EventType = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.EventType, x => x.Count, cancellationToken);
    }

    public async Task<List<(Guid RecipeId, RecipeInteractionEventType EventType, DateTime CreatedAt)>> GetUserInteractionSummaryAsync(
        Guid userId,
        DateTime? since = null,
        CancellationToken cancellationToken = default)
    {
        var query = context.RecipeInteractions
            .Where(ri => ri.UserId == userId);

        if (since.HasValue)
            query = query.Where(ri => ri.CreatedAt >= since.Value);

        return await query
            .OrderByDescending(ri => ri.CreatedAt)
            .Select(ri => new ValueTuple<Guid, RecipeInteractionEventType, DateTime>(
                ri.RecipeId, ri.EventType, ri.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
