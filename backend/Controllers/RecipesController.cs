using backend.Dtos;
using backend.Dtos.Recipes;
using backend.Extensions;
using backend.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/recipes")]
[Authorize]
public class RecipesController(
    IRecipeService recipeService,
    IRecipeRepository recipeRepository,
    ILogger<RecipesController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ApiResponse<RecipeDetailDto>>> CreateAsync(
        [FromBody] CreateRecipeRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetClerkUserId(out var clerkUserId, out var failureReason))
        {
            logger.LogWarning("Create recipe rejected: {Reason}", failureReason ?? "Missing Clerk user id.");
            return Unauthorized(ApiResponse<RecipeDetailDto>.Fail(401, "Could not determine Clerk user id from token."));
        }

        var result = await recipeService.CreateAsync(request, clerkUserId!, cancellationToken);

        return result.Status switch
        {
            CreateRecipeResultStatus.Success => LogAndReturnSuccess(result.Recipe!),
            CreateRecipeResultStatus.UserNotFound => Unauthorized(ApiResponse<RecipeDetailDto>.Fail(401, "User not found.")),
            CreateRecipeResultStatus.HouseholdNotFound => BadRequest(ApiResponse<RecipeDetailDto>.Fail(400, "No household found for current user.")),
            CreateRecipeResultStatus.InvalidRequest => BadRequest(ApiResponse<RecipeDetailDto>.Fail(400, "Invalid recipe payload.")),
            _ => StatusCode(500, ApiResponse<RecipeDetailDto>.Fail(500, "Failed to create recipe."))
        };

        ActionResult<ApiResponse<RecipeDetailDto>> LogAndReturnSuccess(RecipeDetailDto recipe)
        {
            logger.LogInformation("Created recipe {RecipeId} for Clerk user {ClerkUserId}", recipe.Id, clerkUserId);
            return Ok(ApiResponse<RecipeDetailDto>.Success(recipe));
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> DeleteAsync(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetClerkUserId(out var clerkUserId, out var failureReason))
        {
            logger.LogWarning("Delete recipe rejected: {Reason}", failureReason ?? "Missing Clerk user id.");
            return Unauthorized(ApiResponse.Fail(401, "Could not determine Clerk user id from token."));
        }

        var result = await recipeService.DeleteAsync(id, clerkUserId!, cancellationToken);
        return result.Status switch
        {
            DeleteRecipeResultStatus.Success => Ok(ApiResponse.Success()),
            DeleteRecipeResultStatus.UserNotFound => Unauthorized(ApiResponse.Fail(401, "User not found.")),
            DeleteRecipeResultStatus.RecipeNotFound => NotFound(ApiResponse.Fail(404, "Recipe not found.")),
            DeleteRecipeResultStatus.Unauthorized => StatusCode(403, ApiResponse.Fail(403, "Not authorized to delete this recipe.")),
            _ => StatusCode(500, ApiResponse.Fail(500, "Failed to delete recipe."))
        };
    }

    [HttpPut("{recipeId:guid}")]
    public async Task<ActionResult<ApiResponse<RecipeDetailDto>>> UpdateAsync(
        [FromRoute] Guid recipeId,
        [FromBody] CreateRecipeRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetClerkUserId(out var clerkUserId, out var failureReason))
        {
            logger.LogWarning("Update recipe rejected: {Reason}", failureReason ?? "Missing Clerk user id.");
            return Unauthorized(ApiResponse<RecipeDetailDto>.Fail(401, "Could not determine Clerk user id from token."));
        }

        var result = await recipeService.UpdateAsync(recipeId, request, clerkUserId!, cancellationToken);

        return result.Status switch
        {
            UpdateRecipeResultStatus.Success => Ok(ApiResponse<RecipeDetailDto>.Success(result.Recipe!)),
            UpdateRecipeResultStatus.UserNotFound => Unauthorized(ApiResponse<RecipeDetailDto>.Fail(401, result.FailureReason ?? "User not found.")),
            UpdateRecipeResultStatus.RecipeNotFound => NotFound(ApiResponse<RecipeDetailDto>.Fail(404, result.FailureReason ?? "Recipe not found.")),
            UpdateRecipeResultStatus.Unauthorized => StatusCode(403, ApiResponse<RecipeDetailDto>.Fail(403, result.FailureReason ?? "Not authorized to update this recipe.")),
            UpdateRecipeResultStatus.InvalidRequest => BadRequest(ApiResponse<RecipeDetailDto>.Fail(400, result.FailureReason ?? "Invalid recipe payload.")),
            _ => StatusCode(500, ApiResponse<RecipeDetailDto>.Fail(500, "Failed to update recipe."))
        };
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<RecipeCardDto>>>> ListAsync(
        [FromQuery] string? scope,
        CancellationToken cancellationToken)
    {
        var result = await recipeService.ListAsync(scope, User, cancellationToken);

        return result.Status switch
        {
            RecipeListResultStatus.Success => Ok(ApiResponse<IReadOnlyList<RecipeCardDto>>.Success(result.Recipes!)),
            RecipeListResultStatus.InvalidScope => BadRequest(ApiResponse<IReadOnlyList<RecipeCardDto>>.Fail(400, "Invalid scope parameter.")),
            RecipeListResultStatus.MissingClerkUserId => Unauthorized(ApiResponse<IReadOnlyList<RecipeCardDto>>.Fail(401, "Could not determine Clerk user id.")),
            RecipeListResultStatus.UserNotFound => Unauthorized(ApiResponse<IReadOnlyList<RecipeCardDto>>.Fail(401, "User not found.")),
            _ => StatusCode(500, ApiResponse<IReadOnlyList<RecipeCardDto>>.Fail(500, "Failed to list recipes."))
        };
    }

    [HttpGet("{recipeId:guid}")]
    public async Task<ActionResult<ApiResponse<RecipeDetailDto>>> GetByIdAsync(
        [FromRoute] Guid recipeId,
        CancellationToken cancellationToken)
    {
        var result = await recipeService.GetByIdAsync(recipeId, User, cancellationToken);

        return result.Status switch
        {
            RecipeDetailResultStatus.Success =>
                Ok(ApiResponse<RecipeDetailDto>.Success(result.Recipe!)),
            RecipeDetailResultStatus.MissingClerkUserId =>
                Unauthorized(ApiResponse<RecipeDetailDto>.Fail(401, result.FailureReason ?? "Could not determine Clerk user id.")),
            RecipeDetailResultStatus.UserNotFound =>
                Unauthorized(ApiResponse<RecipeDetailDto>.Fail(401, result.FailureReason ?? "User not found.")),
            RecipeDetailResultStatus.Unauthorized =>
                StatusCode(403, ApiResponse<RecipeDetailDto>.Fail(403, result.FailureReason ?? "Not authorized to view this recipe.")),
            RecipeDetailResultStatus.RecipeNotFound =>
                NotFound(ApiResponse<RecipeDetailDto>.Fail(404, result.FailureReason ?? "Recipe not found.")),
            _ => StatusCode(500, ApiResponse<RecipeDetailDto>.Fail(500, "Failed to load recipe details."))
        };
    }

    /// <summary>
    /// Get featured community recipes for the homepage carousel.
    /// </summary>
    /// <param name="count">Number of recipes to return (default 10, max 20).</param>
    [HttpGet("featured")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<FeaturedRecipeDto>>>> GetFeaturedAsync(
        [FromQuery] int count = 10,
        CancellationToken cancellationToken = default)
    {
        if (count < 1 || count > 20)
        {
            return BadRequest(ApiResponse<List<FeaturedRecipeDto>>.Fail(400, "Count must be between 1 and 20."));
        }

        var recipes = await recipeRepository.GetFeaturedRecipesAsync(count, cancellationToken);

        var dtos = recipes.Select(r => new FeaturedRecipeDto(
            r.Id,
            r.Title,
            r.Description,
            r.ImageUrls?.FirstOrDefault(),
            r.TotalTimeMinutes,
            r.Difficulty,
            r.Servings,
            r.LikesCount,
            r.SavedCount,
            r.AuthorId,
            r.Author?.Nickname,
            r.Author?.AvatarUrl,
            r.Tags.Select(t => t.Tag.DisplayName ?? t.Tag.Name).ToList()
        )).ToList();

        if (dtos.Count == 0)
        {
            logger.LogInformation("No featured community recipes found.");
            return Ok(ApiResponse<List<FeaturedRecipeDto>>.Success(dtos, message: "No featured recipes available."));
        }

        logger.LogInformation("Found {Count} featured community recipes.", dtos.Count);
        return Ok(ApiResponse<List<FeaturedRecipeDto>>.Success(dtos));
    }
}
