namespace backend.Dtos.Recipes;

public sealed record CommentListResponseDto(
    Guid RecipeId,
    int TotalCount,
    IReadOnlyList<CommentDto> Items);

