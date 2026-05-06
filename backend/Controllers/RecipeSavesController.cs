using backend.Dtos;
using backend.Dtos.Recipes;
using backend.Extensions;
using backend.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/recipes/{recipeId:guid}/saves")]
[Authorize]
public class RecipeSavesController(
    IRecipeSaveService recipeSaveService,
    ILogger<RecipeSavesController> logger) : ControllerBase
{
    [HttpPost("toggle")]
    public async Task<ActionResult<ApiResponse<RecipeSaveResponseDto>>> ToggleSaveAsync([FromRoute] Guid recipeId,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetClerkUserId(out var clerkUserId, out var failureReason))
        {
            logger.LogWarning("Rejected toggle save for recipe {RecipeId}: {Reason}", recipeId,
                failureReason ?? "Missing Clerk user id claim.");
            return Unauthorized(ApiResponse<RecipeSaveResponseDto>.Fail(401, "Could not determine Clerk user id from token."));
        }

        var response = await recipeSaveService.ToggleSaveAsync(recipeId, clerkUserId!, cancellationToken);
        if (response is null)
        {
            return NotFound(ApiResponse<RecipeSaveResponseDto>.Fail(404, "User or recipe not found."));
        }

        return Ok(ApiResponse<RecipeSaveResponseDto>.Success(response));
    }

    [HttpGet("/api/me/saves")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<MySavedRecipeCardDto>>>> GetMySavedRecipes(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? category = null,
        CancellationToken cancellationToken = default)
    {
        if (!User.TryGetClerkUserId(out var clerkUserId, out var failureReason))
        {
            logger.LogWarning("Rejected get my saves: {Reason}", failureReason ?? "Missing Clerk user id claim.");
            return Unauthorized(ApiResponse<IReadOnlyList<MySavedRecipeCardDto>>.Fail(401, "Could not determine Clerk user id from token."));
        }

        var savesCategory = ParseCategory(category);
        var savedRecipes = await recipeSaveService.GetMySavedRecipesAsync(clerkUserId!, page, pageSize, savesCategory, cancellationToken);
        if (savedRecipes is null)
        {
            return Unauthorized(ApiResponse<IReadOnlyList<MySavedRecipeCardDto>>.Fail(401, "User not found."));
        }

        return Ok(ApiResponse<IReadOnlyList<MySavedRecipeCardDto>>.Success(savedRecipes));
    }

    [HttpGet("/api/me/saves/count")]
    public async Task<ActionResult<ApiResponse<MeSavesCountDto>>> GetMySavesCount(
        [FromQuery] string? category = null,
        CancellationToken cancellationToken = default)
    {
        if (!User.TryGetClerkUserId(out var clerkUserId, out var failureReason))
        {
            logger.LogWarning("Rejected get my saves count: {Reason}", failureReason ?? "Missing Clerk user id claim.");
            return Unauthorized(ApiResponse<MeSavesCountDto>.Fail(401, "Could not determine Clerk user id from token."));
        }

        var savesCategory = ParseCategory(category);
        var count = await recipeSaveService.GetMySavesCountAsync(clerkUserId!, savesCategory, cancellationToken);
        if (!count.HasValue)
        {
            return Unauthorized(ApiResponse<MeSavesCountDto>.Fail(401, "User not found."));
        }

        return Ok(ApiResponse<MeSavesCountDto>.Success(new MeSavesCountDto(count.Value)));
    }

    private static SavesCategory ParseCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return SavesCategory.All;

        return category.Trim().ToLowerInvariant() switch
        {
            "recommended" => SavesCategory.Recommended,
            "community" => SavesCategory.Community,
            "generated" => SavesCategory.Generated,
            _ => SavesCategory.All
        };
    }
}
