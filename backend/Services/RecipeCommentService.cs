using backend.Dtos.Recipes;
using backend.Interfaces;
using backend.Models;

namespace backend.Services;

public class RecipeCommentService(
    IRecipeCommentRepository recipeCommentRepository,
    IUserRepository userRepository,
    IRecipeRepository recipeRepository,
    ILogger<RecipeCommentService> logger) : IRecipeCommentService
{
    public async Task<CommentListResponseDto?> GetRecipeCommentsAsync(Guid recipeId, string? clerkUserId,
        CancellationToken cancellationToken = default)
    {
        var recipeExists = await recipeCommentRepository.RecipeExistsAsync(recipeId, cancellationToken);
        if (!recipeExists)
        {
            logger.LogWarning("Recipe comments rejected: Recipe {RecipeId} not found.", recipeId);
            return null;
        }

        Guid? currentUserId = null;
        if (!string.IsNullOrWhiteSpace(clerkUserId))
        {
            var currentUser = await userRepository.GetByClerkUserIdAsync(clerkUserId, cancellationToken);
            currentUserId = currentUser?.Id;
        }

        var totalCount = await recipeCommentRepository.CountByRecipeIdAsync(recipeId, cancellationToken);
        var items = await recipeCommentRepository.ListByRecipeIdAsync(recipeId, currentUserId, cancellationToken);

        var dtos = items
            .Select(item => new CommentDto(
                item.Comment.Id,
                item.Comment.RecipeId,
                item.Comment.UserId,
                item.AuthorNickname,
                item.AuthorAvatarUrl,
                item.Comment.Content,
                item.Comment.CreatedAt,
                currentUserId.HasValue && item.Comment.UserId == currentUserId.Value,
                item.LikeCount,
                item.IsLikedByCurrentUser))
            .ToList();

        return new CommentListResponseDto(recipeId, totalCount, dtos);
    }

    public async Task<CreateCommentResult> CreateRecipeCommentAsync(Guid recipeId, string clerkUserId, string content,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByClerkUserIdAsync(clerkUserId, cancellationToken);
        if (user is null)
        {
            logger.LogWarning("Create comment rejected: Clerk user {ClerkUserId} not found.", clerkUserId);
            return new CreateCommentResult(CreateCommentResultStatus.UserNotFound);
        }

        var recipe = await recipeRepository.GetByIdAsync(recipeId, cancellationToken);
        if (recipe is null || recipe.Type != RecipeType.User)
        {
            logger.LogWarning("Create comment rejected: Recipe {RecipeId} not found.", recipeId);
            return new CreateCommentResult(CreateCommentResultStatus.RecipeNotFound);
        }

        var now = DateTime.UtcNow;
        var comment = await recipeCommentRepository.AddAsync(recipe.Id, user.Id, content, now, cancellationToken);

        return new CreateCommentResult(CreateCommentResultStatus.Success, new CommentDto(
            comment.Id,
            comment.RecipeId,
            comment.UserId,
            user.Nickname,
            user.AvatarUrl,
            comment.Content,
            comment.CreatedAt,
            CanDelete: true));
    }

    public async Task<DeleteCommentResult> DeleteCommentAsync(Guid commentId, string clerkUserId,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByClerkUserIdAsync(clerkUserId, cancellationToken);
        if (user is null)
        {
            logger.LogWarning("Delete comment rejected: Clerk user {ClerkUserId} not found.", clerkUserId);
            return DeleteCommentResult.UserNotFound;
        }

        var comment = await recipeCommentRepository.GetByIdAsync(commentId, cancellationToken);
        if (comment is null)
        {
            return DeleteCommentResult.CommentNotFound;
        }

        if (comment.UserId != user.Id)
        {
            return DeleteCommentResult.Forbidden;
        }

        var now = DateTime.UtcNow;
        await recipeCommentRepository.DeleteAsync(comment, now, cancellationToken);

        return DeleteCommentResult.Deleted;
    }

    public async Task<ToggleLikeResult> ToggleLikeAsync(Guid commentId, string clerkUserId,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByClerkUserIdAsync(clerkUserId, cancellationToken);
        if (user is null)
        {
            logger.LogWarning("Toggle like rejected: Clerk user {ClerkUserId} not found.", clerkUserId);
            return new ToggleLikeResult(ToggleLikeResultStatus.UserNotFound);
        }

        var comment = await recipeCommentRepository.GetByIdAsync(commentId, cancellationToken);
        if (comment is null)
        {
            logger.LogWarning("Toggle like rejected: Comment {CommentId} not found.", commentId);
            return new ToggleLikeResult(ToggleLikeResultStatus.CommentNotFound);
        }

        var existingLike = await recipeCommentRepository.GetLikeAsync(commentId, user.Id, cancellationToken);
        var now = DateTime.UtcNow;
        bool isLiked;

        if (existingLike is not null)
        {
            await recipeCommentRepository.RemoveLikeAsync(commentId, user.Id, cancellationToken);
            isLiked = false;
        }
        else
        {
            await recipeCommentRepository.AddLikeAsync(commentId, user.Id, now, cancellationToken);
            isLiked = true;
        }

        var likeCount = await recipeCommentRepository.GetLikeCountAsync(commentId, cancellationToken);

        return new ToggleLikeResult(
            ToggleLikeResultStatus.Success,
            new ToggleCommentLikeResponseDto(commentId, isLiked, likeCount));
    }
}

