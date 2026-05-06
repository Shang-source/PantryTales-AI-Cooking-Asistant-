using backend.Data;
using backend.Interfaces;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Repository;

public class RecipeRepository(AppDbContext context) : IRecipeRepository
{
    public async Task<Recipe?> GetByIdAsync(Guid recipeId, CancellationToken cancellationToken = default)
    {
        return await context.Recipes
            .FirstOrDefaultAsync(recipe => recipe.Id == recipeId, cancellationToken);
    }

    public async Task<Recipe?> GetDetailedByIdAsync(Guid recipeId, CancellationToken cancellationToken = default)
    {
        return await context.Recipes
            .AsNoTracking()
            .Include(recipe => recipe.Author)
            .Include(recipe => recipe.Tags)
            .ThenInclude(recipeTag => recipeTag.Tag)
            .Include(recipe => recipe.Ingredients)
            .ThenInclude(recipeIngredient => recipeIngredient.Ingredient)
            .ThenInclude(ingredient => ingredient.Units)
            .Include(recipe => recipe.Ingredients)
            .ThenInclude(recipeIngredient => recipeIngredient.Ingredient)
            .ThenInclude(ingredient => ingredient.Tags)
            .ThenInclude(ingredientTag => ingredientTag.Tag)
            .Include(recipe => recipe.Ingredients)
            .ThenInclude(recipeIngredient => recipeIngredient.Tags)
            .ThenInclude(recipeIngredientTag => recipeIngredientTag.Tag)
            .FirstOrDefaultAsync(recipe => recipe.Id == recipeId, cancellationToken);
    }

    public async Task<List<Recipe>> GetFeaturedRecipesAsync(int count, CancellationToken cancellationToken = default)
    {
        return await context.Recipes
            .Include(r => r.Author)
            .Include(r => r.Tags)
            .ThenInclude(rt => rt.Tag)
            .AsNoTracking()
            .Where(r => r.IsFeatured && r.Visibility == RecipeVisibility.Public && r.Type == RecipeType.User)
            .OrderBy(_ => EF.Functions.Random())
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task ToggleFeaturedAsync(Guid recipeId, CancellationToken cancellationToken = default)
    {
        var recipe = await context.Recipes
            .FirstOrDefaultAsync(r => r.Id == recipeId, cancellationToken);

        if (recipe is not null)
        {
            recipe.IsFeatured = !recipe.IsFeatured;
            recipe.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
