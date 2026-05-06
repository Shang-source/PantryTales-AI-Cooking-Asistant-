using backend.Models;

namespace backend.Interfaces;

public interface IChecklistRepository
{
    Task<List<ChecklistItem>> GetByHouseholdAsync(Guid householdId, CancellationToken cancellationToken = default);
    Task<int> GetCountByHouseholdAsync(Guid householdId, CancellationToken cancellationToken = default);
    Task<int> GetCheckedCountByHouseholdAsync(Guid householdId, CancellationToken cancellationToken = default);
    Task<ChecklistItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(ChecklistItem item, CancellationToken cancellationToken = default);
    Task AddRangeAsync(List<ChecklistItem> items, CancellationToken cancellationToken = default);
    Task UpdateAsync(ChecklistItem item, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> DeleteCheckedAsync(Guid householdId, CancellationToken cancellationToken = default);
    Task<int> DeleteAllAsync(Guid householdId, CancellationToken cancellationToken = default);
}
