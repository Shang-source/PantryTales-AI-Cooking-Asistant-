using System.Security.Claims;
using backend.Dtos.Recipes;

namespace backend.Interfaces;

public interface IRecipeService
{
    Task<CreateRecipeResult> CreateAsync(CreateRecipeRequestDto request, string clerkUserId,
        CancellationToken cancellationToken = default);

    Task<RecipeListResult> ListAsync(string? scope, ClaimsPrincipal user, CancellationToken cancellationToken = default);

    Task<RecipeDetailResult> GetByIdAsync(Guid recipeId, ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<DeleteRecipeResult> DeleteAsync(Guid recipeId, string clerkUserId,
        CancellationToken cancellationToken = default);

    Task<UpdateRecipeResult> UpdateAsync(
        Guid recipeId,
        CreateRecipeRequestDto request,
        string clerkUserId,
        CancellationToken cancellationToken = default);
}
