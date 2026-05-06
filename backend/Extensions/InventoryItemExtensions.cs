using backend.Dtos.Inventory;
using backend.Models;

namespace backend.Extensions;

public static class InventoryItemExtensions
{
    public static InventoryItemResponseDto ToResponseDto(this InventoryItem item)
    {
        return new InventoryItemResponseDto(
            item.Id,
            item.HouseholdId,
            item.IngredientId,
            item.Name,
            item.NormalizedName,
            item.ResolveStatus,
            item.ResolveConfidence,
            item.ResolveMethod,
            item.ResolvedAt,
            item.ResolveAttempts,
            item.LastResolveError,
            item.Amount,
            item.Unit,
            item.StorageMethod,
            CalculateDaysRemaining(item.ExpirationDate),
            item.CreatedAt
        );
    }

    private static int? CalculateDaysRemaining(DateOnly? expirationDate)
    {
        if (expirationDate is null) return null;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return expirationDate.Value.DayNumber - today.DayNumber;
    }
}