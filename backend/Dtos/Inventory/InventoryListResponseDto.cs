using System.Text.Json.Serialization;
using backend.Models;

namespace backend.Dtos.Inventory;

public sealed record InventoryListResponseDto (
    IReadOnlyList<InventoryItemResponseDto> Data,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages =>
        (int)Math.Ceiling((double)TotalCount / PageSize);
}
