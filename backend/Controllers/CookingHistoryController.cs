using backend.Dtos;
using backend.Dtos.Recipes;
using backend.Extensions;
using backend.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Authorize]
public class CookingHistoryController(
    IRecipeCookService recipeCookService,
    ILogger<CookingHistoryController> logger) : ControllerBase
{
    /// <summary>
    /// Record that the user completed cooking a recipe
    /// </summary>
    [HttpPost("api/recipes/{recipeId:guid}/cook/complete")]
    public async Task<ActionResult<ApiResponse<RecipeCookResponseDto>>> RecordCookComplete(
        [FromRoute] Guid recipeId,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetClerkUserId(out var clerkUserId, out var failureReason))
        {
            logger.LogWarning("Rejected record cook for recipe {RecipeId}: {Reason}",
                recipeId, failureReason ?? "Missing Clerk user id claim.");
            return Unauthorized(ApiResponse<RecipeCookResponseDto>.Fail(401,
                "Could not determine Clerk user id from token."));
        }

        var response = await recipeCookService.RecordCookAsync(recipeId, clerkUserId!, cancellationToken);
        if (response is null)
        {
            return NotFound(ApiResponse<RecipeCookResponseDto>.Fail(404, "User or recipe not found."));
        }

        return Ok(ApiResponse<RecipeCookResponseDto>.Success(response));
    }

    /// <summary>
    /// Get user's cooking history (sorted by cook count descending)
    /// </summary>
    [HttpGet("api/me/cooks")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<MyCookedRecipeCardDto>>>> GetMyCookedRecipes(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (!User.TryGetClerkUserId(out var clerkUserId, out var failureReason))
        {
            logger.LogWarning("Rejected get cooking history: {Reason}",
                failureReason ?? "Missing Clerk user id claim.");
            return Unauthorized(ApiResponse<IReadOnlyList<MyCookedRecipeCardDto>>.Fail(401,
                "Could not determine Clerk user id from token."));
        }

        var cookedRecipes = await recipeCookService.GetMyCookedRecipesAsync(
            clerkUserId!, page, pageSize, search, cancellationToken);

        if (cookedRecipes is null)
        {
            return Unauthorized(ApiResponse<IReadOnlyList<MyCookedRecipeCardDto>>.Fail(401, "User not found."));
        }

        return Ok(ApiResponse<IReadOnlyList<MyCookedRecipeCardDto>>.Success(cookedRecipes));
    }

    /// <summary>
    /// Get count of unique recipes the user has cooked
    /// </summary>
    [HttpGet("api/me/cooks/count")]
    public async Task<ActionResult<ApiResponse<MeCooksCountDto>>> GetMyCooksCount(
        CancellationToken cancellationToken)
    {
        if (!User.TryGetClerkUserId(out var clerkUserId, out var failureReason))
        {
            logger.LogWarning("Rejected get cooking count: {Reason}",
                failureReason ?? "Missing Clerk user id claim.");
            return Unauthorized(ApiResponse<MeCooksCountDto>.Fail(401,
                "Could not determine Clerk user id from token."));
        }

        var count = await recipeCookService.GetMyCooksCountAsync(clerkUserId!, cancellationToken);
        if (!count.HasValue)
        {
            return Unauthorized(ApiResponse<MeCooksCountDto>.Fail(401, "User not found."));
        }

        return Ok(ApiResponse<MeCooksCountDto>.Success(new MeCooksCountDto(count.Value)));
    }

    /// <summary>
    /// Delete a specific cooking history entry
    /// </summary>
    [HttpDelete("api/me/cooks/{cookId:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteCookEntry(
        [FromRoute] Guid cookId,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetClerkUserId(out var clerkUserId, out var failureReason))
        {
            logger.LogWarning("Rejected delete cook entry {CookId}: {Reason}",
                cookId, failureReason ?? "Missing Clerk user id claim.");
            return Unauthorized(ApiResponse<object>.Fail(401,
                "Could not determine Clerk user id from token."));
        }

        var success = await recipeCookService.DeleteCookEntryAsync(cookId, clerkUserId!, cancellationToken);
        if (!success)
        {
            return NotFound(ApiResponse<object>.Fail(404, "Cook entry not found or access denied."));
        }

        return Ok(ApiResponse.Success());
    }

    /// <summary>
    /// Clear all cooking history for the user
    /// </summary>
    [HttpDelete("api/me/cooks")]
    public async Task<ActionResult<ApiResponse<object>>> ClearAllCookHistory(
        CancellationToken cancellationToken)
    {
        if (!User.TryGetClerkUserId(out var clerkUserId, out var failureReason))
        {
            logger.LogWarning("Rejected clear cooking history: {Reason}",
                failureReason ?? "Missing Clerk user id claim.");
            return Unauthorized(ApiResponse<object>.Fail(401,
                "Could not determine Clerk user id from token."));
        }

        var success = await recipeCookService.ClearAllCookHistoryAsync(clerkUserId!, cancellationToken);
        if (!success)
        {
            return Unauthorized(ApiResponse<object>.Fail(401, "User not found."));
        }

        return Ok(ApiResponse.Success());
    }
}
