using backend.Dtos;
using backend.Extensions;
using backend.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

/// <summary>
/// Controller for retrieving personalized recipe recommendations.
/// </summary>
[ApiController]
[Route("api/recipes/recommended")]
[Authorize]
public class RecommendedRecipesController(
    IRecommendedRecipeService recommendedRecipeService,
    IUserService userService,
    ILogger<RecommendedRecipesController> logger) : ControllerBase
{
    /// <summary>
    /// Get personalized recipe recommendations for the current user.
    /// </summary>
    /// <param name="limit">Maximum number of recipes to return.</param>
    /// <param name="offset">Offset for pagination.</param>
    /// <param name="search">Optional search term to filter by title or tags.</param>
    /// <param name="seed">
    /// Optional seed used to stabilize the randomized ordering across pagination.
    /// Provide a stable value for the duration of a refresh or session (for example, a short random string).
    /// </param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<RecommendedRecipesResponse>>> GetRecommendations(
        [FromQuery] int limit = 20,
        [FromQuery] int offset = 0,
        [FromQuery] string? search = null,
        [FromQuery] string? seed = null,
        CancellationToken cancellationToken = default)
    {
        if (!User.TryGetClerkUserId(out var clerkUserId, out var failureReason))
        {
            logger.LogWarning("Recommended recipes request failed: {Reason}", failureReason);
            return Unauthorized(ApiResponse<RecommendedRecipesResponse>.Fail(401, "Could not determine user from token."));
        }

        var user = await userService.GetByClerkUserIdAsync(clerkUserId!, cancellationToken);
        if (user == null)
        {
            return Unauthorized(ApiResponse<RecommendedRecipesResponse>.Fail(401, "User not found."));
        }

        var result = await recommendedRecipeService.GetRecommendationsAsync(
            user.Id,
            limit,
            offset,
            search,
            seed,
            cancellationToken);

        if (result.Status == RecommendedRecipeResultStatus.UserNotFound)
        {
            return NotFound(ApiResponse<RecommendedRecipesResponse>.Fail(404, result.ErrorMessage ?? "User not found"));
        }

        if (result.Status == RecommendedRecipeResultStatus.NoRecipesAvailable)
        {
            return Ok(ApiResponse<RecommendedRecipesResponse>.Success(
                new RecommendedRecipesResponse([], 0, "No recipes available yet")));
        }

        logger.LogInformation("Returning {Count} recommendations for user {UserId}",
            result.Recipes?.Count ?? 0, user.Id);

        return Ok(ApiResponse<RecommendedRecipesResponse>.Success(
            new RecommendedRecipesResponse(result.Recipes ?? [], result.TotalCount, null)));
    }
}

/// <summary>
/// Response containing recommended recipes.
/// </summary>
public record RecommendedRecipesResponse(
    IReadOnlyList<RecommendedRecipeDto> Recipes,
    int TotalCount,
    string? Message);
