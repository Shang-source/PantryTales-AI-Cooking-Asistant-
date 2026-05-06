using System.ComponentModel.DataAnnotations;
using backend.Models;

namespace backend.Dtos.Inventory;

public sealed class InventoryListRequestDto
{
    public string? Keyword { get; init; }
    public InventoryStorageMethod? StorageMethod { get; init; }
    public InventorySortBy SortBy { get; init; } = InventorySortBy.Expiring;
    public SortOrder SortOrder { get; init; } = SortOrder.Asc;

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;
}

public enum InventorySortBy
{
    Expiring,
    DateAdded,
    Name
}

public enum SortOrder
{
    Asc,
    Desc
}
