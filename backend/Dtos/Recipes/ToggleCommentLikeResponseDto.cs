namespace backend.Dtos.Recipes;

public sealed record ToggleCommentLikeResponseDto(
    Guid CommentId,
    bool IsLiked,
    int LikeCount);

