using backend.Data;
using backend.Interfaces;
using backend.Models;
using Microsoft.EntityFrameworkCore;
using backend.Dtos.Inventory;

namespace backend.Repository;

public class InventoryRepository(AppDbContext context) : IInventoryRepository
{
    public async Task<InventoryItem> AddAsync(InventoryItem item, CancellationToken cancellationToken = default)
    {
        await context.InventoryItems.AddAsync(item, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return item;
    }

    public async Task AddRangeAsync(IEnumerable<InventoryItem> items, CancellationToken cancellationToken)
    {
        await context.InventoryItems.AddRangeAsync(items, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<InventoryItem>, int)> QueryAsync(
        Guid householdId,
        InventoryListRequestDto query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        IQueryable<InventoryItem> q = context.InventoryItems
            .Where(i => i.HouseholdId == householdId && i.Status == InventoryItemStatus.Active);

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            q = q.Where(i => i.Name.Contains(query.Keyword));
        }

        if (query.StorageMethod.HasValue)
        {
            q = q.Where(i => i.StorageMethod == query.StorageMethod.Value);
        }

        q = query.SortBy switch
        {
            InventorySortBy.Expiring =>
                query.SortOrder == SortOrder.Asc
                    ? q
                        .OrderBy(i => i.ExpirationDate == null)
                        .ThenBy(i => i.ExpirationDate)
                    : q
                        .OrderBy(i => i.ExpirationDate == null)
                        .ThenByDescending(i => i.ExpirationDate),

            InventorySortBy.Name =>
                query.SortOrder == SortOrder.Asc
                    ? q.OrderBy(i => i.Name.ToLower())
                    : q.OrderByDescending(i => i.Name.ToLower()),

            _ =>
                query.SortOrder == SortOrder.Asc
                    ? q.OrderBy(i => i.CreatedAt)
                    : q.OrderByDescending(i => i.CreatedAt)
        };

        var totalCount = await q.CountAsync(cancellationToken);

        var items = await q
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<InventoryStatsResponseDto> GetStatsAsync(
        Guid householdId,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var expiringBy = today.AddDays(3);

        var query = context.InventoryItems
            .Where(i => i.HouseholdId == householdId && i.Status == InventoryItemStatus.Active);

        var stats = await query
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalCount = g.Count(),
                ExpiringCount = g.Count(i =>
                    i.ExpirationDate != null &&
                    i.ExpirationDate >= today &&
                    i.ExpirationDate <= expiringBy),
                LocationCount = g
                    .Select(i => i.StorageMethod)
                    .Distinct()
                    .Count()
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (stats == null)
        {
            return new InventoryStatsResponseDto(0, 0, 0);
        }

        return new InventoryStatsResponseDto(
            stats.TotalCount,
            stats.ExpiringCount,
            stats.LocationCount);
    }

    public async Task<InventoryItem?> GetByIdAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        return await context.InventoryItems
            .FirstOrDefaultAsync(item => item.Id == itemId, cancellationToken);
    }

    public async Task<InventoryItem> UpdateAsync(InventoryItem item, CancellationToken cancellationToken = default)
    {
        context.InventoryItems.Update(item);
        await context.SaveChangesAsync(cancellationToken);
        return item;
    }

    public async Task<bool> DeleteAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        var affectedRows = await context.InventoryItems
            .Where(item => item.Id == itemId)
            .ExecuteDeleteAsync(cancellationToken);
        return affectedRows > 0;
    }
}
