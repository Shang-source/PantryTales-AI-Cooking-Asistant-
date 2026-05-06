using System.Reflection.Metadata.Ecma335;
using Amazon.S3.Model;
using backend.Dtos.Inventory;
using backend.Dtos;
using backend.Extensions;
using backend.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/inventory")]
[Authorize]
public class InventoryController(IInventoryService InventoryService, ILogger<InventoryController> logger) : ControllerBase
{
    //create inventory item
    [HttpPost]
    public async Task<ActionResult<ApiResponse<InventoryItemResponseDto>>> CreateInventoryItemAsync(
        [FromBody] CreateInventoryItemRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetClerkUserId(out var clerkUserId, out var failureReason))
        {
            logger.LogWarning("Rejected profile lookup: {Reason}", failureReason ?? "Missing Clerk user id claim.");
            return Unauthorized(ApiResponse.Fail(401, "Could not determine Clerk user id from token."));
        }

        var createResult = await InventoryService.CreateInventoryItemAsync(clerkUserId!, request, cancellationToken);

        return createResult.Result switch
        {
            InventoryError.None =>
                Ok(ApiResponse<InventoryItemResponseDto>.Success(createResult.Data!)),
            InventoryError.NoActiveHousehold =>
                Unauthorized(ApiResponse.Fail(401, "No active household.")),
            _ =>
                StatusCode(500, ApiResponse.Fail(500, "An unexpected error occurred.")),
        };
    }

    //batch create inventory item
    [HttpPost("batch")]
    public async Task<ActionResult<ApiResponse<List<InventoryItemResponseDto>>>> CreateBatchAsync(
        [FromBody] List<CreateInventoryItemRequestDto> items,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetClerkUserId(out var clerkUserId, out var failureReason))
        {
            logger.LogWarning("Rejected profile lookup: {Reason}", failureReason ?? "Missing Clerk user id claim.");
            return Unauthorized(ApiResponse.Fail(401, "Could not determine Clerk user id from token."));
        }

        if (items == null || items.Count == 0)
        {
            return BadRequest(ApiResponse.Fail(400, "At least one inventory item is required."));
        }

        var createdItems = await InventoryService
            .CreateBatchAsync(clerkUserId!, items, cancellationToken);

        return createdItems.Result switch
        {
            InventoryError.None =>
                Ok(ApiResponse<List<InventoryItemResponseDto>>.Success(createdItems.Data!)),
            InventoryError.NoActiveHousehold => 
                Unauthorized(ApiResponse.Fail(401, "No active household.")),
            _ =>
                StatusCode(500, ApiResponse.Fail(500, "An unexpected error occurred.")),
        };
    }

    //get inventory items list
    [HttpGet]
    public async Task<ActionResult<ApiResponse<InventoryListResponseDto>>> GetInventoryAsync(
        [FromQuery] InventoryListRequestDto query,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetClerkUserId(out var clerkUserId, out var failureReason))
        {
            logger.LogWarning("Rejected profile lookup: {Reason}", failureReason ?? "Missing Clerk user id claim.");
            return Unauthorized(ApiResponse.Fail(401, "Could not determine Clerk user id from token."));
        }

        var result = await InventoryService.GetInventoryListAsync(clerkUserId!, query, cancellationToken);

        return result.Result switch
        {
            InventoryError.None =>
                Ok(ApiResponse<InventoryListResponseDto>.Success(result.Data!)),
            InventoryError.NoActiveHousehold =>
                Unauthorized(ApiResponse.Fail(401, "No active household.")),
            _ =>
                StatusCode(500, ApiResponse.Fail(500, "Unexpected error"))
        };
    }

    //get inventory stats
    [HttpGet("stats")]
    public async Task<ActionResult<ApiResponse<InventoryStatsResponseDto>>> GetInventoryStatsAsync(
        CancellationToken cancellationToken)
    {
        if (!User.TryGetClerkUserId(out var clerkUserId, out var failureReason))
        {
            logger.LogWarning("Rejected profile lookup: {Reason}", failureReason ?? "Missing Clerk user id claim.");
            return Unauthorized(ApiResponse.Fail(401, "Could not determine Clerk user id from token."));
        }

        var result = await InventoryService.GetInventoryStatsAsync(clerkUserId!, cancellationToken);

        return result.Result switch
        {
            InventoryError.None =>
                Ok(ApiResponse<InventoryStatsResponseDto>.Success(result.Data!)),
            InventoryError.NoActiveHousehold =>
                Unauthorized(ApiResponse.Fail(401, "No active household.")),
            _ =>
                StatusCode(500, ApiResponse.Fail(500, "Unexpected error"))
        };
    }

    //update inventory item
    [HttpPatch("{itemId:guid}")]
    public async Task<ActionResult<ApiResponse<InventoryItemResponseDto>>> UpdateInventoryItemAsync(
        [FromRoute] Guid itemId,
        [FromBody] UpdateInventoryItemRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetClerkUserId(out var clerkUserId, out var failureReason))
        {
            logger.LogWarning("Rejected profile lookup: {Reason}", failureReason ?? "Missing Clerk user id claim.");
            return Unauthorized(ApiResponse.Fail(401, "Could not determine Clerk user id from token."));
        }

        var updateResult = await InventoryService.UpdateInventoryItemAsync(itemId, clerkUserId!, request, cancellationToken);

        return updateResult.Result switch
        {
            InventoryError.None =>
                Ok(ApiResponse<InventoryItemResponseDto>.Success(updateResult.Data!)),
            InventoryError.ItemNotFound =>
                NotFound(ApiResponse.Fail(404, "Inventory item not found.")),
            InventoryError.NoActiveHousehold =>
                Unauthorized(ApiResponse.Fail(401, "No active household.")),
            InventoryError.HouseholdMismatch =>
                StatusCode(403, ApiResponse.Fail(403, "You do not have the permission to update this item.")),
            _ =>
                StatusCode(500, ApiResponse.Fail(500, "Unexpected inventory error."))
        };
    }

    //delete inventory item
    [HttpDelete("{itemId:guid}")]
    public async Task<ActionResult<ApiResponse>> DeleteInventoryItemAsync(
        [FromRoute] Guid itemId,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetClerkUserId(out var clerkUserId, out var failureReason))
        {
            logger.LogWarning("Rejected profile lookup: {Reason}", failureReason ?? "Missing Clerk user id claim.");
            return Unauthorized(ApiResponse.Fail(401, "Could not determine Clerk user id from token."));
        }

        var result = await InventoryService.DeleteInventoryItemAsync(itemId, clerkUserId!, cancellationToken);

        return result.Result switch
        {
            InventoryError.None =>
                Ok(ApiResponse.Success("Inventory item deleted.")),
            InventoryError.ItemNotFound =>
                NotFound(ApiResponse.Fail(404, "Inventory item not found.")),
            InventoryError.HouseholdMismatch =>
                StatusCode(403, ApiResponse.Fail(403, "You do not have permission to delete this item.")),
            InventoryError.NoActiveHousehold =>
                Unauthorized(ApiResponse.Fail(401, "No active household.")),
            _ =>
                StatusCode(500, ApiResponse.Fail(500, "Unexpected inventory error."))
        };
    }

    /// <summary>
    /// Deduct inventory items based on a recipe's ingredients.
    /// Called when completing a cooking session.
    /// </summary>
    [HttpPost("deduct-for-recipe")]
    public async Task<ActionResult<ApiResponse<DeductionResult>>> DeductForRecipeAsync(
        [FromBody] DeductForRecipeRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetClerkUserId(out var clerkUserId, out var failureReason))
        {
            logger.LogWarning("Rejected deduct for recipe: {Reason}", failureReason ?? "Missing Clerk user id claim.");
            return Unauthorized(ApiResponse.Fail(401, "Could not determine Clerk user id from token."));
        }

        var result = await InventoryService.DeductForRecipeAsync(
            clerkUserId!,
            request.RecipeId,
            request.Servings,
            cancellationToken);

        return result.Result switch
        {
            InventoryError.None =>
                Ok(ApiResponse<DeductionResult>.Success(result.Data!)),
            InventoryError.NoActiveHousehold =>
                Unauthorized(ApiResponse.Fail(401, "No active household.")),
            InventoryError.IngredientNotFound =>
                NotFound(ApiResponse.Fail(404, "Recipe not found.")),
            _ =>
                StatusCode(500, ApiResponse.Fail(500, "Unexpected error."))
        };
    }
}
