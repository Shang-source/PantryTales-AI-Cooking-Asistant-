namespace backend.Dtos.Recipes;

public sealed record MyLikedRecipeCardDto(
    Guid Id,
    string Title,
    string? Description,
    string? CoverImageUrl,
    Guid? AuthorId,
    string AuthorName,
    int LikesCount,
    bool LikedByMe,
    DateTime LikedAt);
