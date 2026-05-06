namespace backend.Dtos.Recipes;

public sealed record RecipeLikeResponseDto(
    Guid RecipeId,
    bool IsLiked,
    int LikesCount
);
