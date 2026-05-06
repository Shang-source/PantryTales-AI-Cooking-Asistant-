using backend.Dtos;
using backend.Dtos.Interactions;
using backend.Extensions;
using backend.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/interactions")]
[Authorize]
public class RecipeInteractionController(
    IRecipeInteractionService interactionService,
    ILogger<RecipeInteractionController> logger) : ControllerBase
{
    /// <summary>
    /// Log a single recipe interaction event (click, open, save, like, cook, share, dwell).
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> LogInteractionAsync(
        [FromBody] LogInteractionRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetClerkUserId(out var clerkUserId, out var failureReason))
        {
            logger.LogWarning("Rejected interaction: {Reason}", failureReason ?? "Missing Clerk user id.");
            return Unauthorized(ApiResponse.Fail(401, "Could not determine user from token."));
        }

        var success = await interactionService.LogInteractionAsync(
            clerkUserId!,
            request.RecipeId,
            request.EventType,
            request.Source,
            request.SessionId,
            request.DwellSeconds,
            cancellationToken);

        if (!success)
        {
            return NotFound(ApiResponse.Fail(404, "User or recipe not found."));
        }

        return Ok(ApiResponse<object>.Success(new { }, message: "Interaction logged."));
    }

    /// <summary>
    /// Batch log impression events (call when recipes are displayed in feed).
    /// </summary>
    [HttpPost("impressions")]
    public async Task<ActionResult<ApiResponse<object>>> LogImpressionsAsync(
        [FromBody] LogImpressionsRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetClerkUserId(out var clerkUserId, out var failureReason))
        {
            logger.LogWarning("Rejected impressions: {Reason}", failureReason ?? "Missing Clerk user id.");
            return Unauthorized(ApiResponse.Fail(401, "Could not determine user from token."));
        }

        if (request.RecipeIds.Count == 0)
        {
            return BadRequest(ApiResponse.Fail(400, "RecipeIds cannot be empty."));
        }

        if (request.RecipeIds.Count > 100)
        {
            return BadRequest(ApiResponse.Fail(400, "Cannot log more than 100 impressions at once."));
        }

        var count = await interactionService.LogImpressionsAsync(
            clerkUserId!,
            request.RecipeIds,
            request.Source,
            request.SessionId,
            cancellationToken);

        logger.LogDebug("Logged {Count} impressions for user {ClerkUserId}.", count, clerkUserId);

        return Ok(ApiResponse<object>.Success(new { count }, message: $"Logged {count} impressions."));
    }

    /// <summary>
    /// Get interaction stats for a recipe (admin/analytics use).
    /// </summary>
    [HttpGet("stats/{recipeId:guid}")]
    public async Task<ActionResult<ApiResponse<RecipeInteractionStatsDto>>> GetStatsAsync(
        Guid recipeId,
        [FromQuery] int days = 30,
        CancellationToken cancellationToken = default)
    {
        if (days < 1 || days > 365)
        {
            return BadRequest(ApiResponse.Fail(400, "Days must be between 1 and 365."));
        }

        var stats = await interactionService.GetRecipeStatsAsync(recipeId, days, cancellationToken);

        if (stats is null)
        {
            return NotFound(ApiResponse.Fail(404, "Recipe not found."));
        }


        logger.LogInformation("Successfully retrieved stats for recipe {RecipeId} for the past {Days} days.", recipeId, days);

        return Ok(ApiResponse<RecipeInteractionStatsDto>.Success(stats));
    }
}
