namespace backend.Dtos.Recipes;

public sealed record CommentDto(
    Guid Id,
    Guid RecipeId,
    Guid UserId,
    string AuthorNickname,
    string? AuthorAvatarUrl,
    string Content,
    DateTime CreatedAt,
    bool CanDelete,
    int LikeCount = 0,
    bool IsLikedByCurrentUser = false);

