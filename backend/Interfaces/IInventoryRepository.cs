using backend.Models;
using backend.Dtos.Inventory;

namespace backend.Interfaces;

public interface IInventoryRepository
{
    Task<InventoryItem> AddAsync(InventoryItem item, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<InventoryItem> items, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<InventoryItem>, int)> QueryAsync(Guid householdId, InventoryListRequestDto query, int page, int pageSize, CancellationToken cancellationToken);
    Task<InventoryStatsResponseDto> GetStatsAsync(Guid householdId, CancellationToken cancellationToken);
    Task<InventoryItem?> GetByIdAsync(Guid itemId, CancellationToken cancellationToken = default);
    Task<InventoryItem> UpdateAsync(InventoryItem item, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid itemId, CancellationToken cancellationToken = default);
}
