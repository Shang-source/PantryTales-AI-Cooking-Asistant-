using backend.Dtos;
using backend.Dtos.Recipes;
using backend.Extensions;
using backend.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/recipes/{recipeId:guid}/likes")]
[Authorize]
public class RecipeLikesController(
    IRecipeLikeService recipeLikeService,
    ILogger<RecipeLikesController> logger) : ControllerBase
{
    [HttpPost("toggle")]
    public async Task<ActionResult<ApiResponse<RecipeLikeResponseDto>>> ToggleLikeAsync([FromRoute] Guid recipeId,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetClerkUserId(out var clerkUserId, out var failureReason))
        {
            logger.LogWarning("Rejected toggle like for recipe {RecipeId}: {Reason}", recipeId,
                failureReason ?? "Missing Clerk user id claim.");
            return Unauthorized(ApiResponse<RecipeLikeResponseDto>.Fail(401, "Could not determine Clerk user id from token."));
        }

        var response = await recipeLikeService.ToggleLikeAsync(recipeId, clerkUserId!, cancellationToken);
        if (response is null)
        {
            return NotFound(ApiResponse<RecipeLikeResponseDto>.Fail(404, "User or recipe not found."));
        }

        return Ok(ApiResponse<RecipeLikeResponseDto>.Success(response));
    }

    [HttpGet("/api/me/likes")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<MyLikedRecipeCardDto>>>> GetMyLikedRecipes(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!User.TryGetClerkUserId(out var clerkUserId, out var failureReason))
        {
            logger.LogWarning("Rejected get my likes: {Reason}", failureReason ?? "Missing Clerk user id claim.");
            return Unauthorized(ApiResponse<IReadOnlyList<MyLikedRecipeCardDto>>.Fail(401, "Could not determine Clerk user id from token."));
        }

        var likedRecipes = await recipeLikeService.GetMyLikedRecipesAsync(clerkUserId!, page, pageSize, cancellationToken);
        if (likedRecipes is null)
        {
            return Unauthorized(ApiResponse<IReadOnlyList<MyLikedRecipeCardDto>>.Fail(401, "User not found."));
        }

        return Ok(ApiResponse<IReadOnlyList<MyLikedRecipeCardDto>>.Success(likedRecipes));
    }

    [HttpGet("/api/me/likes/count")]
    public async Task<ActionResult<ApiResponse<MeLikesCountDto>>> GetMyLikesCount(CancellationToken cancellationToken)
    {
        if (!User.TryGetClerkUserId(out var clerkUserId, out var failureReason))
        {
            logger.LogWarning("Rejected get my likes count: {Reason}", failureReason ?? "Missing Clerk user id claim.");
            return Unauthorized(ApiResponse<MeLikesCountDto>.Fail(401, "Could not determine Clerk user id from token."));
        }

        var count = await recipeLikeService.GetMyLikesCountAsync(clerkUserId!, cancellationToken);
        if (!count.HasValue)
        {
            return Unauthorized(ApiResponse<MeLikesCountDto>.Fail(401, "User not found."));
        }

        return Ok(ApiResponse<MeLikesCountDto>.Success(new MeLikesCountDto(count.Value)));
    }
}
