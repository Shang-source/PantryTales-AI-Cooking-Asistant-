using backend.Dtos.Recipes;

namespace backend.Interfaces;

public interface IRecipeCommentService
{
    Task<CommentListResponseDto?> GetRecipeCommentsAsync(Guid recipeId, string? clerkUserId,
        CancellationToken cancellationToken = default);

    Task<CreateCommentResult> CreateRecipeCommentAsync(Guid recipeId, string clerkUserId, string content,
        CancellationToken cancellationToken = default);

    Task<DeleteCommentResult> DeleteCommentAsync(Guid commentId, string clerkUserId,
        CancellationToken cancellationToken = default);

    Task<ToggleLikeResult> ToggleLikeAsync(Guid commentId, string clerkUserId,
        CancellationToken cancellationToken = default);
}

public sealed record CreateCommentResult(CreateCommentResultStatus Status, CommentDto? Comment = null);

public enum CreateCommentResultStatus
{
    Success,
    UserNotFound,
    RecipeNotFound
}

public enum DeleteCommentResult
{
    Deleted,
    CommentNotFound,
    UserNotFound,
    Forbidden
}

public sealed record ToggleLikeResult(ToggleLikeResultStatus Status, ToggleCommentLikeResponseDto? Data = null);

public enum ToggleLikeResultStatus
{
    Success,
    UserNotFound,
    CommentNotFound
}

