using backend.Data;
using backend.Dtos.Checklist;
using backend.Interfaces;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public class ChecklistService(
    IHouseholdService householdService,
    IChecklistRepository checklistRepository,
    AppDbContext dbContext,
    ISmartRecipeService smartRecipeService,
    ILogger<ChecklistService> logger) : IChecklistService
{
    public async Task<ChecklistResult<ChecklistListDto>> GetItemsAsync(
        string clerkUserId,
        CancellationToken cancellationToken = default)
    {
        var householdId = await householdService.GetActiveHouseholdIdAsync(clerkUserId, cancellationToken);
        if (householdId is null)
        {
            logger.LogWarning("No active household for user {ClerkUserId} when getting checklist.", clerkUserId);
            return ChecklistResult<ChecklistListDto>.Fail(ChecklistError.NoActiveHousehold);
        }

        var items = await checklistRepository.GetByHouseholdAsync(householdId.Value, cancellationToken);
        var dtos = items.Select(MapToDto).ToList();

        return ChecklistResult<ChecklistListDto>.Ok(new ChecklistListDto(dtos, dtos.Count));
    }

    public async Task<ChecklistResult<ChecklistItemDto>> AddItemAsync(
        string clerkUserId,
        CreateChecklistItemDto dto,
        CancellationToken cancellationToken = default)
    {
        var householdId = await householdService.GetActiveHouseholdIdAsync(clerkUserId, cancellationToken);
        if (householdId is null)
        {
            logger.LogWarning("No active household for user {ClerkUserId} when adding checklist item.", clerkUserId);
            return ChecklistResult<ChecklistItemDto>.Fail(ChecklistError.NoActiveHousehold);
        }

        var item = new ChecklistItem
        {
            HouseholdId = householdId.Value,
            Name = dto.Name,
            Amount = dto.Amount,
            Unit = dto.Unit,
            Category = dto.Category,
            FromRecipeId = dto.FromRecipeId,
            IsChecked = false
        };

        await checklistRepository.AddAsync(item, cancellationToken);
        logger.LogInformation("Created checklist item {ItemId} for household {HouseholdId}.", item.Id, householdId);

        return ChecklistResult<ChecklistItemDto>.Ok(MapToDto(item));
    }

    public async Task<ChecklistResult<List<ChecklistItemDto>>> AddBatchAsync(
        string clerkUserId,
        BatchCreateChecklistItemsDto dto,
        CancellationToken cancellationToken = default)
    {
        var householdId = await householdService.GetActiveHouseholdIdAsync(clerkUserId, cancellationToken);
        if (householdId is null)
        {
            logger.LogWarning("No active household for user {ClerkUserId} when batch adding checklist items.", clerkUserId);
            return ChecklistResult<List<ChecklistItemDto>>.Fail(ChecklistError.NoActiveHousehold);
        }

        var items = dto.Items.Select(i => new ChecklistItem
        {
            HouseholdId = householdId.Value,
            Name = i.Name,
            Amount = i.Amount,
            Unit = i.Unit,
            Category = i.Category,
            FromRecipeId = dto.FromRecipeId ?? i.FromRecipeId,
            IsChecked = false
        }).ToList();

        await checklistRepository.AddRangeAsync(items, cancellationToken);
        logger.LogInformation("Created {Count} checklist items for household {HouseholdId}.", items.Count, householdId);

        return ChecklistResult<List<ChecklistItemDto>>.Ok(items.Select(MapToDto).ToList());
    }

    public async Task<ChecklistResult<ChecklistItemDto>> UpdateItemAsync(
        Guid id,
        string clerkUserId,
        UpdateChecklistItemDto dto,
        CancellationToken cancellationToken = default)
    {
        var authResult = await GetAuthorizedItemAsync(id, clerkUserId, cancellationToken);
        if (!authResult.IsSuccess)
        {
            return ChecklistResult<ChecklistItemDto>.Fail(authResult.Error);
        }

        var item = authResult.Data!;

        if (dto.Name is not null) item.Name = dto.Name;
        if (dto.Amount.HasValue) item.Amount = dto.Amount.Value;
        if (dto.Unit is not null) item.Unit = dto.Unit;
        if (dto.Category is not null) item.Category = dto.Category;
        if (dto.IsChecked.HasValue) item.IsChecked = dto.IsChecked.Value;
        item.UpdatedAt = DateTime.UtcNow;

        await checklistRepository.UpdateAsync(item, cancellationToken);
        logger.LogInformation("Updated checklist item {ItemId}.", id);

        return ChecklistResult<ChecklistItemDto>.Ok(MapToDto(item));
    }

    public async Task<ChecklistActionResult> DeleteItemAsync(
        Guid id,
        string clerkUserId,
        CancellationToken cancellationToken = default)
    {
        var authResult = await GetAuthorizedItemAsync(id, clerkUserId, cancellationToken);
        if (!authResult.IsSuccess)
        {
            return ChecklistActionResult.Fail(authResult.Error);
        }

        var deleted = await checklistRepository.DeleteAsync(id, cancellationToken);
        if (!deleted)
        {
            return ChecklistActionResult.Fail(ChecklistError.ItemNotFound);
        }

        logger.LogInformation("Deleted checklist item {ItemId}.", id);
        return ChecklistActionResult.Ok();
    }

    public async Task<ChecklistResult<int>> ClearCheckedAsync(
        string clerkUserId,
        CancellationToken cancellationToken = default)
    {
        var householdId = await householdService.GetActiveHouseholdIdAsync(clerkUserId, cancellationToken);
        if (householdId is null)
        {
            logger.LogWarning("No active household for user {ClerkUserId} when clearing checked items.", clerkUserId);
            return ChecklistResult<int>.Fail(ChecklistError.NoActiveHousehold);
        }

        var deletedCount = await checklistRepository.DeleteCheckedAsync(householdId.Value, cancellationToken);
        logger.LogInformation("Cleared {Count} checked items from household {HouseholdId}.", deletedCount, householdId);

        return ChecklistResult<int>.Ok(deletedCount);
    }

    public async Task<ChecklistResult<int>> ClearAllAsync(
        string clerkUserId,
        CancellationToken cancellationToken = default)
    {
        var householdId = await householdService.GetActiveHouseholdIdAsync(clerkUserId, cancellationToken);
        if (householdId is null)
        {
            logger.LogWarning("No active household for user {ClerkUserId} when clearing all items.", clerkUserId);
            return ChecklistResult<int>.Fail(ChecklistError.NoActiveHousehold);
        }

        var deletedCount = await checklistRepository.DeleteAllAsync(householdId.Value, cancellationToken);
        logger.LogInformation("Cleared all {Count} items from household {HouseholdId}.", deletedCount, householdId);

        return ChecklistResult<int>.Ok(deletedCount);
    }

    public async Task<ChecklistResult<ChecklistStatsDto>> GetStatsAsync(
        string clerkUserId,
        CancellationToken cancellationToken = default)
    {
        var householdId = await householdService.GetActiveHouseholdIdAsync(clerkUserId, cancellationToken);
        if (householdId is null)
        {
            logger.LogWarning("No active household for user {ClerkUserId} when getting checklist stats.", clerkUserId);
            return ChecklistResult<ChecklistStatsDto>.Fail(ChecklistError.NoActiveHousehold);
        }

        var totalCount = await checklistRepository.GetCountByHouseholdAsync(householdId.Value, cancellationToken);
        var purchasedCount = await checklistRepository.GetCheckedCountByHouseholdAsync(householdId.Value, cancellationToken);
        var remainingCount = Math.Max(totalCount - purchasedCount, 0);

        return ChecklistResult<ChecklistStatsDto>.Ok(
            new ChecklistStatsDto(totalCount, purchasedCount, remainingCount));
    }

    public async Task<ChecklistResult<MoveToInventoryResultDto>> MoveCheckedToInventoryAsync(
        string clerkUserId,
        CancellationToken cancellationToken = default)
    {
        var householdId = await householdService.GetActiveHouseholdIdAsync(clerkUserId, cancellationToken);
        if (householdId is null)
        {
            logger.LogWarning("No active household for user {ClerkUserId} when moving checked items to inventory.", clerkUserId);
            return ChecklistResult<MoveToInventoryResultDto>.Fail(ChecklistError.NoActiveHousehold);
        }

        var checkedItems = await dbContext.ChecklistItems
            .Where(c => c.HouseholdId == householdId.Value && c.IsChecked)
            .ToListAsync(cancellationToken);

        if (checkedItems.Count == 0)
        {
            return ChecklistResult<MoveToInventoryResultDto>.Fail(ChecklistError.NoCheckedItems);
        }

        var now = DateTime.UtcNow;
        var inventoryItems = checkedItems.Select(ci => new InventoryItem
        {
            Id = Guid.CreateVersion7(),
            HouseholdId = householdId.Value,
            IngredientId = ci.IngredientId,
            Name = ci.Name,
            Amount = ci.Amount,
            Unit = ci.Unit,
            StorageMethod = InventoryStorageMethod.RoomTemp,
            Status = InventoryItemStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        }).ToList();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await dbContext.InventoryItems.AddRangeAsync(inventoryItems, cancellationToken);
            await dbContext.ChecklistItems
                .Where(c => c.HouseholdId == householdId.Value && c.IsChecked)
                .ExecuteDeleteAsync(cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        _ = smartRecipeService.InvalidateForHouseholdAsync(householdId.Value, cancellationToken);

        logger.LogInformation(
            "Moved {Count} checked items to inventory for household {HouseholdId}.",
            inventoryItems.Count, householdId);

        return ChecklistResult<MoveToInventoryResultDto>.Ok(
            new MoveToInventoryResultDto(inventoryItems.Count));
    }

    private async Task<ChecklistResult<ChecklistItem>> GetAuthorizedItemAsync(
        Guid id,
        string clerkUserId,
        CancellationToken cancellationToken)
    {
        var item = await checklistRepository.GetByIdAsync(id, cancellationToken);
        if (item is null)
        {
            logger.LogInformation("Checklist item {ItemId} not found.", id);
            return ChecklistResult<ChecklistItem>.Fail(ChecklistError.ItemNotFound);
        }

        var householdId = await householdService.GetActiveHouseholdIdAsync(clerkUserId, cancellationToken);
        if (householdId is null)
        {
            logger.LogWarning("No active household for user {ClerkUserId}.", clerkUserId);
            return ChecklistResult<ChecklistItem>.Fail(ChecklistError.NoActiveHousehold);
        }

        if (item.HouseholdId != householdId.Value)
        {
            logger.LogWarning("User {ClerkUserId} cannot access checklist item {ItemId}.", clerkUserId, id);
            return ChecklistResult<ChecklistItem>.Fail(ChecklistError.HouseholdMismatch);
        }

        return ChecklistResult<ChecklistItem>.Ok(item);
    }

    private static ChecklistItemDto MapToDto(ChecklistItem item) => new(
        item.Id,
        item.Name,
        item.Amount,
        item.Unit,
        item.Category,
        item.IsChecked,
        item.FromRecipeId,
        item.FromRecipe?.Title,
        item.CreatedAt,
        item.UpdatedAt
    );
}
