using backend.Dtos.Inventory;
using backend.Models;

namespace backend.Extensions;

public static class InventoryItemMappingExtensions
{
    public static InventoryItem ToEntity(
        this CreateInventoryItemRequestDto dto,
        Guid householdId,
        DateTime nowUtc)
    {
        DateOnly? expirationDate = dto.ExpirationDays.HasValue
            ? DateOnly.FromDateTime(nowUtc.AddDays(dto.ExpirationDays.Value))
            : null;

        return new InventoryItem
        {
            Id = Guid.CreateVersion7(),
            HouseholdId = householdId,
            Name = dto.Name,
            Amount = dto.Amount,
            Unit = dto.Unit,
            StorageMethod = dto.StorageMethod,
            ExpirationDate = expirationDate,
            Status = InventoryItemStatus.Active,
            CreatedAt = nowUtc,
            UpdatedAt = nowUtc
        };
    }
}