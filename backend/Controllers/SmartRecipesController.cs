using System.Text.Json;
using backend.Dtos;
using backend.Dtos.SmartRecipes;
using backend.Extensions;
using backend.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

/// <summary>
/// Controller for AI-powered smart recipe suggestions.
/// </summary>
[ApiController]
[Route("api/smart-recipes")]
[Authorize]
public class SmartRecipesController(
    ISmartRecipeService smartRecipeService,
    IUserService userService,
    ILogger<SmartRecipesController> logger) : ControllerBase
{
    /// <summary>
    /// Get smart recipe suggestions for the current user.
    /// Generates new recipes if none exist for today or if inventory changed.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SmartRecipeDto>>>> GetSmartRecipes(
        CancellationToken cancellationToken,
        [FromQuery] bool allowStale = false)
    {
        if (!User.TryGetClerkUserId(out var clerkUserId, out var failureReason))
        {
            logger.LogWarning("Smart recipes request failed: {Reason}", failureReason);
            return Unauthorized(ApiResponse<IReadOnlyList<SmartRecipeDto>>.Fail(401, "Could not determine user from token."));
        }

        var user = await userService.GetByClerkUserIdAsync(clerkUserId!, cancellationToken);
        if (user == null)
        {
            return Unauthorized(ApiResponse<IReadOnlyList<SmartRecipeDto>>.Fail(401, "User not found."));
        }

        var result = await smartRecipeService.GetOrGenerateAsync(user.Id, allowStale, cancellationToken);

        return result.Status switch
        {
            SmartRecipeResultStatus.Success => Ok(ApiResponse<IReadOnlyList<SmartRecipeDto>>.Success(result.Recipes!)),
            SmartRecipeResultStatus.NoInventory => Ok(ApiResponse<IReadOnlyList<SmartRecipeDto>>.Fail(200, "Add items to your inventory to get personalized recipes.")),
            SmartRecipeResultStatus.NoHousehold => BadRequest(ApiResponse<IReadOnlyList<SmartRecipeDto>>.Fail(400, "You need to be part of a household.")),
            SmartRecipeResultStatus.GenerationFailed => StatusCode(500, ApiResponse<IReadOnlyList<SmartRecipeDto>>.Fail(500, result.ErrorMessage ?? "Failed to generate recipes.")),
            _ => StatusCode(500, ApiResponse<IReadOnlyList<SmartRecipeDto>>.Fail(500, "Unexpected error."))
        };
    }

    /// <summary>
    /// Force regeneration of smart recipes.
    /// </summary>
    /// <param name="servings">Optional number of servings to generate recipes for. Defaults to household size.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost("refresh")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SmartRecipeDto>>>> RefreshSmartRecipes(
        [FromQuery] int? servings,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetClerkUserId(out var clerkUserId, out var failureReason))
        {
            logger.LogWarning("Smart recipes refresh failed: {Reason}", failureReason);
            return Unauthorized(ApiResponse<IReadOnlyList<SmartRecipeDto>>.Fail(401, "Could not determine user from token."));
        }

        var user = await userService.GetByClerkUserIdAsync(clerkUserId!, cancellationToken);
        if (user == null)
        {
            return Unauthorized(ApiResponse<IReadOnlyList<SmartRecipeDto>>.Fail(401, "User not found."));
        }

        logger.LogInformation("Force regenerating smart recipes for user {UserId} with servings {Servings}", user.Id, servings);

        var result = await smartRecipeService.ForceRegenerateAsync(user.Id, servings, cancellationToken);

        return result.Status switch
        {
            SmartRecipeResultStatus.Success => Ok(ApiResponse<IReadOnlyList<SmartRecipeDto>>.Success(result.Recipes!)),
            SmartRecipeResultStatus.NoInventory => Ok(ApiResponse<IReadOnlyList<SmartRecipeDto>>.Fail(200, "Add items to your inventory to get personalized recipes.")),
            SmartRecipeResultStatus.NoHousehold => BadRequest(ApiResponse<IReadOnlyList<SmartRecipeDto>>.Fail(400, "You need to be part of a household.")),
            SmartRecipeResultStatus.GenerationFailed => StatusCode(500, ApiResponse<IReadOnlyList<SmartRecipeDto>>.Fail(500, result.ErrorMessage ?? "Failed to generate recipes.")),
            _ => StatusCode(500, ApiResponse<IReadOnlyList<SmartRecipeDto>>.Fail(500, "Unexpected error."))
        };
    }

    /// <summary>
    /// Stream smart recipe generation via Server-Sent Events.
    /// Recipes are sent one-by-one as they are generated.
    /// </summary>
    /// <param name="servings">Optional number of servings to generate recipes for. Defaults to household size.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("stream")]
    public async Task StreamSmartRecipes(
        [FromQuery] int? servings,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetClerkUserId(out var clerkUserId, out var failureReason))
        {
            logger.LogWarning("Smart recipes stream failed: {Reason}", failureReason);
            Response.StatusCode = 401;
            return;
        }

        var user = await userService.GetByClerkUserIdAsync(clerkUserId!, cancellationToken);
        if (user == null)
        {
            Response.StatusCode = 401;
            return;
        }

        // Set SSE headers
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");
        Response.Headers.Append("X-Accel-Buffering", "no"); // Disable nginx buffering

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        logger.LogInformation("Starting SSE stream for smart recipes, user {UserId}, servings {Servings}", user.Id, servings);

        // Add timeout to prevent indefinite connections if AI provider hangs
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        var linkedToken = linkedCts.Token;

        try
        {
            await foreach (var sseEvent in smartRecipeService.StreamGenerateAsync(user.Id, servings, linkedToken))
            {
                var json = JsonSerializer.Serialize(sseEvent, jsonOptions);
                await Response.WriteAsync($"data: {json}\n\n", linkedToken);
                await Response.Body.FlushAsync(linkedToken);
            }
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            logger.LogWarning("SSE stream timed out for user {UserId}", user.Id);
            var errorEvent = new SmartRecipeSseEvent(SmartRecipeSseEventType.Error, ErrorMessage: "Stream timed out");
            var errorJson = JsonSerializer.Serialize(errorEvent, jsonOptions);
            try
            {
                await Response.WriteAsync($"data: {errorJson}\n\n");
                await Response.Body.FlushAsync();
            }
            catch { /* Client may have disconnected */ }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("SSE stream cancelled for user {UserId}", user.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during SSE stream for user {UserId}", user.Id);
            var errorEvent = new SmartRecipeSseEvent(SmartRecipeSseEventType.Error, ErrorMessage: "Streaming failed");
            var errorJson = JsonSerializer.Serialize(errorEvent, jsonOptions);
            await Response.WriteAsync($"data: {errorJson}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }
}
