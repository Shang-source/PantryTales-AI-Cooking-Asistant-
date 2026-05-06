using System.Text.Json.Serialization;
using backend.Models;

namespace backend.Dtos.Inventory;

public sealed record InventoryItemResponseDto(
    Guid Id,
    Guid HouseholdId,
    Guid? IngredientId,
    string Name,
    string? NormalizedName,
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    IngredientResolveStatus ResolveStatus,
    decimal? ResolveConfidence,
    string? ResolveMethod,
    DateTime? ResolvedAt,
    int ResolveAttempts,
    string? LastResolveError,
    decimal Amount,
    string Unit,
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    InventoryStorageMethod StorageMethod,
    int? DaysRemaining,
    DateTime CreatedAt
);