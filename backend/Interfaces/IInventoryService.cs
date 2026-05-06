using backend.Dtos.Inventory;
using backend.Models;

namespace backend.Interfaces;

public interface IInventoryService
{
    Task<InventoryResult<InventoryItemResponseDto>> CreateInventoryItemAsync(string clerkUserId, CreateInventoryItemRequestDto request, CancellationToken cancellationToken = default);
    Task<InventoryResult<List<InventoryItemResponseDto>>> CreateBatchAsync(string clerkUserId, IReadOnlyList<CreateInventoryItemRequestDto> items, CancellationToken cancellationToken = default);
    Task<InventoryResult<InventoryListResponseDto>> GetInventoryListAsync(string clerkUserId, InventoryListRequestDto query, CancellationToken cancellationToken = default);
    Task<InventoryResult<InventoryStatsResponseDto>> GetInventoryStatsAsync(string clerkUserId, CancellationToken cancellationToken = default);
    Task<InventoryResult<InventoryItemResponseDto>> UpdateInventoryItemAsync(Guid itemId, string clerkUserId, UpdateInventoryItemRequestDto request, CancellationToken cancellationToken = default);
    Task<InventoryActionResult> DeleteInventoryItemAsync(Guid itemId, string clerkUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deduct ingredients from inventory based on a recipe's ingredients.
    /// Uses fuzzy matching to find inventory items that match recipe ingredients.
    /// </summary>
    Task<InventoryResult<DeductionResult>> DeductForRecipeAsync(string clerkUserId, Guid recipeId, int servings, CancellationToken cancellationToken = default);
}
