using backend.Data;
using backend.Interfaces;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Repository;

public class ChecklistRepository(AppDbContext context) : IChecklistRepository
{
    public async Task<List<ChecklistItem>> GetByHouseholdAsync(
        Guid householdId,
        CancellationToken cancellationToken = default)
    {
        return await context.ChecklistItems
            .Where(c => c.HouseholdId == householdId)
            .Include(c => c.FromRecipe)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetCountByHouseholdAsync(
        Guid householdId,
        CancellationToken cancellationToken = default)
    {
        return await context.ChecklistItems
            .Where(c => c.HouseholdId == householdId)
            .CountAsync(cancellationToken);
    }

    public async Task<int> GetCheckedCountByHouseholdAsync(
        Guid householdId,
        CancellationToken cancellationToken = default)
    {
        return await context.ChecklistItems
            .Where(c => c.HouseholdId == householdId && c.IsChecked)
            .CountAsync(cancellationToken);
    }

    public async Task<ChecklistItem?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await context.ChecklistItems
            .Include(c => c.FromRecipe)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task AddAsync(
        ChecklistItem item,
        CancellationToken cancellationToken = default)
    {
        await context.ChecklistItems.AddAsync(item, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddRangeAsync(
        List<ChecklistItem> items,
        CancellationToken cancellationToken = default)
    {
        await context.ChecklistItems.AddRangeAsync(items, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(
        ChecklistItem item,
        CancellationToken cancellationToken = default)
    {
        context.ChecklistItems.Update(item);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var affectedRows = await context.ChecklistItems
            .Where(c => c.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
        return affectedRows > 0;
    }

    public async Task<int> DeleteCheckedAsync(
        Guid householdId,
        CancellationToken cancellationToken = default)
    {
        return await context.ChecklistItems
            .Where(c => c.HouseholdId == householdId && c.IsChecked)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<int> DeleteAllAsync(
        Guid householdId,
        CancellationToken cancellationToken = default)
    {
        return await context.ChecklistItems
            .Where(c => c.HouseholdId == householdId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
