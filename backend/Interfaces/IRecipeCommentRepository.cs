using backend.Models;

namespace backend.Interfaces;

public interface IRecipeCommentRepository
{
    Task<bool> RecipeExistsAsync(Guid recipeId, CancellationToken cancellationToken = default);

    Task<int> CountByRecipeIdAsync(Guid recipeId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<(RecipeComment Comment, string AuthorNickname, string? AuthorAvatarUrl, int LikeCount, bool IsLikedByCurrentUser)>> ListByRecipeIdAsync(
        Guid recipeId,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default);

    Task<RecipeComment> AddAsync(Guid recipeId, Guid userId, string content, DateTime now,
        CancellationToken cancellationToken = default);

    Task<RecipeComment?> GetByIdAsync(Guid commentId, CancellationToken cancellationToken = default);

    Task DeleteAsync(RecipeComment comment, DateTime now, CancellationToken cancellationToken = default);

    Task<CommentLike?> GetLikeAsync(Guid commentId, Guid userId, CancellationToken cancellationToken = default);

    Task<int> GetLikeCountAsync(Guid commentId, CancellationToken cancellationToken = default);

    Task<CommentLike> AddLikeAsync(Guid commentId, Guid userId, DateTime now, CancellationToken cancellationToken = default);

    Task<bool> RemoveLikeAsync(Guid commentId, Guid userId, CancellationToken cancellationToken = default);
}
