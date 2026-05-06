using backend.Dtos;
using backend.Dtos.Recipes;
using backend.Extensions;
using backend.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/recipes/{recipeId:guid}/comments")]
public class RecipeCommentsController(
    IRecipeCommentService recipeCommentService,
    ILogger<RecipeCommentsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<CommentListResponseDto>>> GetRecipeCommentsAsync(
        [FromRoute] Guid recipeId,
        CancellationToken cancellationToken)
    {
        string? clerkUserId = null;
        if (User.TryGetClerkUserId(out var foundClerkUserId, out _))
        {
            clerkUserId = foundClerkUserId;
        }

        var response = await recipeCommentService.GetRecipeCommentsAsync(recipeId, clerkUserId, cancellationToken);
        if (response is null)
        {
            return NotFound(ApiResponse<CommentListResponseDto>.Fail(404, "Recipe not found."));
        }

        return Ok(ApiResponse<CommentListResponseDto>.Success(response));
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<ApiResponse<CommentDto>>> CreateRecipeCommentAsync(
        [FromRoute] Guid recipeId,
        [FromBody] CreateCommentRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetClerkUserId(out var clerkUserId, out var failureReason))
        {
            logger.LogWarning("Rejected create comment for recipe {RecipeId}: {Reason}", recipeId,
                failureReason ?? "Missing Clerk user id claim.");
            return Unauthorized(ApiResponse<CommentDto>.Fail(401, "Could not determine Clerk user id from token."));
        }

        var content = request.Content?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(content))
        {
            return BadRequest(ApiResponse<CommentDto>.Fail(400, "Content is required."));
        }

        if (content.Length > 1000)
        {
            return BadRequest(ApiResponse<CommentDto>.Fail(400, "Content must be 1000 characters or less."));
        }

        var result = await recipeCommentService.CreateRecipeCommentAsync(recipeId, clerkUserId!, content, cancellationToken);
        return result.Status switch
        {
            CreateCommentResultStatus.Success => Ok(ApiResponse<CommentDto>.Success(result.Comment!)),
            CreateCommentResultStatus.UserNotFound => Unauthorized(ApiResponse<CommentDto>.Fail(401, "User not found.")),
            CreateCommentResultStatus.RecipeNotFound => NotFound(ApiResponse<CommentDto>.Fail(404, "Recipe not found.")),
            _ => StatusCode(500, ApiResponse<CommentDto>.Fail(500, "Failed to create comment."))
        };
    }

    [HttpDelete("/api/comments/{commentId:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteCommentAsync([FromRoute] Guid commentId, CancellationToken cancellationToken)
    {
        if (!User.TryGetClerkUserId(out var clerkUserId, out var failureReason))
        {
            logger.LogWarning("Rejected delete comment {CommentId}: {Reason}", commentId,
                failureReason ?? "Missing Clerk user id claim.");
            return Unauthorized(ApiResponse.Fail(401, "Could not determine Clerk user id from token."));
        }

        var result = await recipeCommentService.DeleteCommentAsync(commentId, clerkUserId!, cancellationToken);

        return result switch
        {
            DeleteCommentResult.Deleted => Ok(ApiResponse<CommentDeleteResponseDto>.Success(
                new CommentDeleteResponseDto(commentId, true),
                message: "Deleted successfully")),
            DeleteCommentResult.CommentNotFound => NotFound(ApiResponse.Fail(404, "Comment not found.")),
            DeleteCommentResult.UserNotFound => Unauthorized(ApiResponse.Fail(401, "User not found.")),
            DeleteCommentResult.Forbidden => StatusCode(403, ApiResponse.Fail(403, "Not authorized to delete this comment.")),
            _ => StatusCode(500, ApiResponse.Fail(500, "Failed to delete comment."))
        };
    }

    [HttpPost("/api/comments/{commentId:guid}/like")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<ToggleCommentLikeResponseDto>>> ToggleLikeAsync(
        [FromRoute] Guid commentId,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetClerkUserId(out var clerkUserId, out var failureReason))
        {
            logger.LogWarning("Rejected toggle like for comment {CommentId}: {Reason}", commentId,
                failureReason ?? "Missing Clerk user id claim.");
            return Unauthorized(ApiResponse<ToggleCommentLikeResponseDto>.Fail(401, "Could not determine Clerk user id from token."));
        }

        var result = await recipeCommentService.ToggleLikeAsync(commentId, clerkUserId!, cancellationToken);

        return result.Status switch
        {
            ToggleLikeResultStatus.Success => Ok(ApiResponse<ToggleCommentLikeResponseDto>.Success(result.Data!)),
            ToggleLikeResultStatus.UserNotFound => Unauthorized(ApiResponse<ToggleCommentLikeResponseDto>.Fail(401, "User not found.")),
            ToggleLikeResultStatus.CommentNotFound => NotFound(ApiResponse<ToggleCommentLikeResponseDto>.Fail(404, "Comment not found.")),
            _ => StatusCode(500, ApiResponse<ToggleCommentLikeResponseDto>.Fail(500, "Failed to toggle like."))
        };
    }
}
