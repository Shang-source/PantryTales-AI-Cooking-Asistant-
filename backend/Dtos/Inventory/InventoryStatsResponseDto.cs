using backend.Models;

namespace backend.Dtos.Inventory;

public sealed record InventoryStatsResponseDto(
    int TotalCount,
    int ExpiringSoonCount,
    int StorageMethodCount
);
