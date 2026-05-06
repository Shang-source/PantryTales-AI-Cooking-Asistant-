using backend.Dtos.Recipes;

namespace backend.Interfaces;

public interface IRecipeLikeService
{
    Task<RecipeLikeResponseDto?> ToggleLikeAsync(Guid recipeId, string clerkUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MyLikedRecipeCardDto>?> GetMyLikedRecipesAsync(string clerkUserId, int page, int pageSize,
        CancellationToken cancellationToken = default);

    Task<int?> GetMyLikesCountAsync(string clerkUserId, CancellationToken cancellationToken = default);
}
