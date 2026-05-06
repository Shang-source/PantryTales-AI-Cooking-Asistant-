using backend.Dtos;
using backend.Dtos.Checklist;
using backend.Extensions;
using backend.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/checklist")]
[Authorize]
public class ChecklistController(
    IChecklistService checklistService,
    ILogger<ChecklistController> logger) : ControllerBase
{
    /// <summary>
    /// Get all checklist items for the current user's household.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<ChecklistListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ChecklistListDto>>> GetItems(
        CancellationToken cancellationToken)
    {
        if (!User.TryGetClerkUserId(out var clerkUserId, out var failureReason))
        {
            logger.LogWarning("Get checklist rejected: {Reason}", failureReason);
            return Unauthorized(ApiResponse<ChecklistListDto>.Fail(401, "Could not determine user."));
        }

        var result = await checklistService.GetItemsAsync(clerkUserId!, cancellationToken);

        return result.Error switch
        {
            ChecklistError.Success => Ok(ApiResponse<ChecklistListDto>.Success(result.Data!)),
            ChecklistError.NoActiveHousehold => BadRequest(ApiResponse<ChecklistListDto>.Fail(400, "No active household.")),
            _ => StatusCode(500, ApiResponse<ChecklistListDto>.Fail(500, "Failed to get checklist."))
        };
    }

    /// <summary>
    /// Get checklist stats for the current user's household.
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(ApiResponse<ChecklistStatsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ChecklistStatsDto>>> GetStats(
        CancellationToken cancellationToken = default)
    {
        if (!User.TryGetClerkUserId(out var clerkUserId, out var failureReason))
        {
            logger.LogWarning("Get checklist stats rejected: {Reason}", failureReason);
            return Unauthorized(ApiResponse<ChecklistStatsDto>.Fail(401, "Could not determine user."));
        }

        var result = await checklistService.GetStatsAsync(clerkUserId!, cancellationToken);

        return result.Error switch
        {
            ChecklistError.Success => Ok(ApiResponse<ChecklistStatsDto>.Success(result.Data!)),
            ChecklistError.NoActiveHousehold => BadRequest(ApiResponse<ChecklistStatsDto>.Fail(400, "No active household.")),
            _ => StatusCode(500, ApiResponse<ChecklistStatsDto>.Fail(500, "Failed to get checklist stats."))
        };
    }

    /// <summary>
    /// Add a single item to the checklist.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ChecklistItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ChecklistItemDto>>> AddItem(
        [FromBody] CreateChecklistItemDto request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetClerkUserId(out var clerkUserId, out var failureReason))
        {
            logger.LogWarning("Add checklist item rejected: {Reason}", failureReason);
            return Unauthorized(ApiResponse<ChecklistItemDto>.Fail(401, "Could not determine user."));
        }

        var result = await checklistService.AddItemAsync(clerkUserId!, request, cancellationToken);

        return result.Error switch
        {
            ChecklistError.Success => Ok(ApiResponse<ChecklistItemDto>.Success(result.Data!)),
            ChecklistError.NoActiveHousehold => BadRequest(ApiResponse<ChecklistItemDto>.Fail(400, "No active household.")),
            _ => StatusCode(500, ApiResponse<ChecklistItemDto>.Fail(500, "Failed to add item."))
        };
    }

    /// <summary>
    /// Add multiple items to the checklist (e.g., from recipe missing ingredients).
    /// </summary>
    [HttpPost("batch")]
    [ProducesResponseType(typeof(ApiResponse<List<ChecklistItemDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<ChecklistItemDto>>>> AddBatch(
        [FromBody] BatchCreateChecklistItemsDto request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetClerkUserId(out var clerkUserId, out var failureReason))
        {
            logger.LogWarning("Batch add checklist items rejected: {Reason}", failureReason);
            return Unauthorized(ApiResponse<List<ChecklistItemDto>>.Fail(401, "Could not determine user."));
        }

        var result = await checklistService.AddBatchAsync(clerkUserId!, request, cancellationToken);

        return result.Error switch
        {
            ChecklistError.Success => Ok(ApiResponse<List<ChecklistItemDto>>.Success(result.Data!, message: $"Added {result.Data!.Count} items.")),
            ChecklistError.NoActiveHousehold => BadRequest(ApiResponse<List<ChecklistItemDto>>.Fail(400, "No active household.")),
            _ => StatusCode(500, ApiResponse<List<ChecklistItemDto>>.Fail(500, "Failed to add items."))
        };
    }

    /// <summary>
    /// Update a checklist item (name, amount, unit, or checked status).
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ChecklistItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ChecklistItemDto>>> UpdateItem(
        [FromRoute] Guid id,
        [FromBody] UpdateChecklistItemDto request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetClerkUserId(out var clerkUserId, out var failureReason))
        {
            logger.LogWarning("Update checklist item rejected: {Reason}", failureReason);
            return Unauthorized(ApiResponse<ChecklistItemDto>.Fail(401, "Could not determine user."));
        }

        var result = await checklistService.UpdateItemAsync(id, clerkUserId!, request, cancellationToken);

        return result.Error switch
        {
            ChecklistError.Success => Ok(ApiResponse<ChecklistItemDto>.Success(result.Data!)),
            ChecklistError.ItemNotFound => NotFound(ApiResponse<ChecklistItemDto>.Fail(404, "Item not found.")),
            ChecklistError.HouseholdMismatch => StatusCode(403, ApiResponse<ChecklistItemDto>.Fail(403, "Access denied.")),
            ChecklistError.NoActiveHousehold => BadRequest(ApiResponse<ChecklistItemDto>.Fail(400, "No active household.")),
            _ => StatusCode(500, ApiResponse<ChecklistItemDto>.Fail(500, "Failed to update item."))
        };
    }

    /// <summary>
    /// Delete a checklist item.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> DeleteItem(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetClerkUserId(out var clerkUserId, out var failureReason))
        {
            logger.LogWarning("Delete checklist item rejected: {Reason}", failureReason);
            return Unauthorized(ApiResponse.Fail(401, "Could not determine user."));
        }

        var result = await checklistService.DeleteItemAsync(id, clerkUserId!, cancellationToken);

        return result.Error switch
        {
            ChecklistError.Success => Ok(ApiResponse.Success("Item deleted.")),
            ChecklistError.ItemNotFound => NotFound(ApiResponse.Fail(404, "Item not found.")),
            ChecklistError.HouseholdMismatch => StatusCode(403, ApiResponse.Fail(403, "Access denied.")),
            ChecklistError.NoActiveHousehold => BadRequest(ApiResponse.Fail(400, "No active household.")),
            _ => StatusCode(500, ApiResponse.Fail(500, "Failed to delete item."))
        };
    }

    /// <summary>
    /// Move all checked items from checklist to inventory.
    /// </summary>
    [HttpPost("move-to-inventory")]
    [ProducesResponseType(typeof(ApiResponse<MoveToInventoryResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<MoveToInventoryResultDto>>> MoveToInventory(
        CancellationToken cancellationToken)
    {
        if (!User.TryGetClerkUserId(out var clerkUserId, out var failureReason))
        {
            logger.LogWarning("Move to inventory rejected: {Reason}", failureReason);
            return Unauthorized(ApiResponse<MoveToInventoryResultDto>.Fail(401, "Could not determine user."));
        }

        var result = await checklistService.MoveCheckedToInventoryAsync(clerkUserId!, cancellationToken);

        return result.Error switch
        {
            ChecklistError.Success => Ok(ApiResponse<MoveToInventoryResultDto>.Success(result.Data!, message: $"Moved {result.Data!.ItemsMoved} items to inventory.")),
            ChecklistError.NoActiveHousehold => BadRequest(ApiResponse<MoveToInventoryResultDto>.Fail(400, "No active household.")),
            ChecklistError.NoCheckedItems => BadRequest(ApiResponse<MoveToInventoryResultDto>.Fail(400, "No checked items to move.")),
            _ => StatusCode(500, ApiResponse<MoveToInventoryResultDto>.Fail(500, "Failed to move items to inventory."))
        };
    }

    /// <summary>
    /// Clear all checked items from the checklist.
    /// </summary>
    [HttpDelete("clear-checked")]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<int>>> ClearChecked(
        CancellationToken cancellationToken)
    {
        if (!User.TryGetClerkUserId(out var clerkUserId, out var failureReason))
        {
            logger.LogWarning("Clear checked items rejected: {Reason}", failureReason);
            return Unauthorized(ApiResponse<int>.Fail(401, "Could not determine user."));
        }

        var result = await checklistService.ClearCheckedAsync(clerkUserId!, cancellationToken);

        return result.Error switch
        {
            ChecklistError.Success => Ok(ApiResponse<int>.Success(result.Data, message: $"Cleared {result.Data} items.")),
            ChecklistError.NoActiveHousehold => BadRequest(ApiResponse<int>.Fail(400, "No active household.")),
            _ => StatusCode(500, ApiResponse<int>.Fail(500, "Failed to clear items."))
        };
    }

    /// <summary>
    /// Clear all items from the checklist.
    /// </summary>
    [HttpDelete("clear-all")]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<int>>> ClearAll(
        CancellationToken cancellationToken)
    {
        if (!User.TryGetClerkUserId(out var clerkUserId, out var failureReason))
        {
            logger.LogWarning("Clear all items rejected: {Reason}", failureReason);
            return Unauthorized(ApiResponse<int>.Fail(401, "Could not determine user."));
        }

        var result = await checklistService.ClearAllAsync(clerkUserId!, cancellationToken);

        return result.Error switch
        {
            ChecklistError.Success => Ok(ApiResponse<int>.Success(result.Data, message: $"Cleared {result.Data} items.")),
            ChecklistError.NoActiveHousehold => BadRequest(ApiResponse<int>.Fail(400, "No active household.")),
            _ => StatusCode(500, ApiResponse<int>.Fail(500, "Failed to clear items."))
        };
    }
}
