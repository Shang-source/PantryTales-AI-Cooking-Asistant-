using backend.Auth;
using backend.Data;
using backend.Dtos.Admin;
using backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers.Admin;

[ApiController]
[Route("api/admin/recipes")]
[RequireAdmin]
public class RecipesAdminController(
    AppDbContext dbContext,
    ILogger<RecipesAdminController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AdminRecipeListResponseDto>> ListAsync(
        [FromQuery] AdminRecipeQueryParameters queryParameters,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Recipes
            .Include(r => r.Author)
            .AsNoTracking()
            .AsQueryable();

        if (queryParameters.Type.HasValue)
        {
            query = query.Where(r => r.Type == queryParameters.Type.Value);
        }

        if (queryParameters.Visibility.HasValue)
        {
            query = query.Where(r => r.Visibility == queryParameters.Visibility.Value);
        }

        if (!string.IsNullOrWhiteSpace(queryParameters.Search))
        {
            var search = $"%{queryParameters.Search.Trim()}%";
            query = query.Where(r => EF.Functions.ILike(r.Title, search));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .ThenBy(r => r.Id)
            .Skip((queryParameters.Page - 1) * queryParameters.PageSize)
            .Take(queryParameters.PageSize)
            .Select(r => new AdminRecipeListItemDto(
                r.Id,
                r.Title,
                r.Description,
                r.Type,
                r.Visibility,
                r.AuthorId,
                r.Author != null ? r.Author.Nickname : null,
                r.LikesCount,
                r.CommentsCount,
                r.SavedCount,
                r.Difficulty,
                r.CreatedAt,
                r.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Ok(new AdminRecipeListResponseDto(
            total,
            queryParameters.Page,
            queryParameters.PageSize,
            items));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminRecipeListItemDto>> GetAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var recipe = await dbContext.Recipes
            .Include(r => r.Author)
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => new AdminRecipeListItemDto(
                r.Id,
                r.Title,
                r.Description,
                r.Type,
                r.Visibility,
                r.AuthorId,
                r.Author != null ? r.Author.Nickname : null,
                r.LikesCount,
                r.CommentsCount,
                r.SavedCount,
                r.Difficulty,
                r.CreatedAt,
                r.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        if (recipe is null)
        {
            return NotFound();
        }

        return Ok(recipe);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AdminRecipeListItemDto>> UpdateAsync(
        Guid id,
        [FromBody] AdminUpdateRecipeRequestDto request,
        CancellationToken cancellationToken)
    {
        var recipe = await dbContext.Recipes
            .Include(r => r.Author)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (recipe is null)
        {
            return NotFound();
        }

        var normalizedTitle = request.Title.Trim();
        if (string.IsNullOrWhiteSpace(normalizedTitle))
        {
            return ValidationProblem("Title is required.");
        }

        recipe.Title = normalizedTitle;
        recipe.Description = request.Description?.Trim();
        recipe.Type = request.Type;
        recipe.Visibility = request.Visibility;
        recipe.Difficulty = request.Difficulty;
        recipe.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Admin updated recipe {RecipeId}: Title={Title}, Type={Type}, Visibility={Visibility}",
            id, recipe.Title, recipe.Type, recipe.Visibility);

        return Ok(new AdminRecipeListItemDto(
            recipe.Id,
            recipe.Title,
            recipe.Description,
            recipe.Type,
            recipe.Visibility,
            recipe.AuthorId,
            recipe.Author?.Nickname,
            recipe.LikesCount,
            recipe.CommentsCount,
            recipe.SavedCount,
            recipe.Difficulty,
            recipe.CreatedAt,
            recipe.UpdatedAt));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var recipe = await dbContext.Recipes.FindAsync([id], cancellationToken);
        if (recipe is null)
        {
            return NotFound();
        }

        dbContext.Recipes.Remove(recipe);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Admin deleted recipe {RecipeId}: Title={Title}", id, recipe.Title);

        return NoContent();
    }
}
