using backend.Models;

namespace backend.Interfaces;

public interface IRecipeSaveRepository
{
    Task<RecipeSave?> GetRecipeSaveAsync(Guid userId, Guid recipeId, CancellationToken cancellationToken = default);
    Task AddRecipeSaveAsync(RecipeSave recipeSave, CancellationToken cancellationToken = default);
    Task RemoveRecipeSaveAsync(RecipeSave recipeSave, CancellationToken cancellationToken = default);
    Task<int> IncrementRecipeSavedCountAsync(Guid recipeId, DateTime updatedAt, CancellationToken cancellationToken = default);
    Task<int> DecrementRecipeSavedCountAsync(Guid recipeId, DateTime updatedAt, CancellationToken cancellationToken = default);
    Task<int?> GetRecipeSavedCountAsync(Guid recipeId, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

