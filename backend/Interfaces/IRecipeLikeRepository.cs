using backend.Models;

namespace backend.Interfaces;

public interface IRecipeLikeRepository
{
    Task<RecipeLike?> GetRecipeLikeAsync(Guid userId, Guid recipeId, CancellationToken cancellationToken = default);
    Task AddRecipeLikeAsync(RecipeLike recipeLike, CancellationToken cancellationToken = default);
    Task RemoveRecipeLikeAsync(RecipeLike recipeLike, CancellationToken cancellationToken = default);
    Task<int> IncrementRecipeLikesCountAsync(Guid recipeId, DateTime updatedAt, CancellationToken cancellationToken = default);
    Task<int> DecrementRecipeLikesCountAsync(Guid recipeId, DateTime updatedAt, CancellationToken cancellationToken = default);
    Task<int?> GetRecipeLikesCountAsync(Guid recipeId, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
