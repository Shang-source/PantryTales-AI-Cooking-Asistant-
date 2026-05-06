namespace backend.Dtos.Recipes;

public sealed record CommentDeleteResponseDto(Guid CommentId, bool Deleted);

