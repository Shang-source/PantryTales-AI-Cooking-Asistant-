using System.ComponentModel.DataAnnotations;
using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Pages.Admin.Recipes;

public class IndexModel(AppDbContext dbContext) : AdminPageModel
{
    private const int MaxPageSize = 100;

    public List<RecipeListItem> Recipes { get; set; } = [];
    public int TotalRecipes { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalRecipes / (double)PageSize);

    [BindProperty(SupportsGet = true)]
    public RecipeType? FilterType { get; set; }

    [BindProperty(SupportsGet = true)]
    public RecipeVisibility? FilterVisibility { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true, Name = "p")]
    [Range(1, int.MaxValue)]
    public int CurrentPage { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    [Range(1, MaxPageSize)]
    public int PageSize { get; set; } = 20;

    [BindProperty]
    public Guid EditId { get; set; }

    [BindProperty]
    public EditRecipeModel EditRecipe { get; set; } = new();

    public async Task OnGetAsync()
    {
        PageSize = Math.Clamp(PageSize, 1, MaxPageSize);
        await LoadDataAsync();
    }

    public async Task<IActionResult> OnPostEditAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadDataAsync();
            return Page();
        }

        var recipe = await dbContext.Recipes.FindAsync(EditId);
        if (recipe == null)
        {
            TempData["Error"] = "Recipe not found.";
            return RedirectToPage();
        }

        recipe.Title = EditRecipe.Title.Trim();
        recipe.Description = EditRecipe.Description?.Trim();
        recipe.Type = EditRecipe.Type;
        recipe.Visibility = EditRecipe.Visibility;
        recipe.Difficulty = EditRecipe.Difficulty;
        recipe.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();
        TempData["Success"] = "Recipe updated successfully.";
        return RedirectToPage("./Index", new { p = CurrentPage, PageSize, FilterType, FilterVisibility, Search });
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        var recipe = await dbContext.Recipes.FindAsync(id);
        if (recipe != null)
        {
            dbContext.Recipes.Remove(recipe);
            await dbContext.SaveChangesAsync();
            TempData["Success"] = "Recipe deleted successfully.";
        }
        else
        {
            TempData["Error"] = "Recipe not found.";
        }
        return RedirectToPage("./Index", new { p = CurrentPage, PageSize, FilterType, FilterVisibility, Search });
    }

    public async Task<IActionResult> OnPostToggleFeaturedAsync(Guid id)
    {
        var recipe = await dbContext.Recipes.FindAsync(id);
        if (recipe != null)
        {
            // Only allow featuring public User-type recipes
            if (recipe.Type != RecipeType.User || recipe.Visibility != RecipeVisibility.Public)
            {
                TempData["Error"] = "Only public community recipes can be featured.";
                return RedirectToPage("./Index", new { p = CurrentPage, PageSize, FilterType, FilterVisibility, Search });
            }

            recipe.IsFeatured = !recipe.IsFeatured;
            recipe.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync();
            TempData["Success"] = recipe.IsFeatured ? "Recipe featured successfully." : "Recipe unfeatured successfully.";
        }
        return RedirectToPage("./Index", new { p = CurrentPage, PageSize, FilterType, FilterVisibility, Search });
    }

    private async Task LoadDataAsync()
    {
        var query = dbContext.Recipes
            .Include(r => r.Author)
            .AsNoTracking()
            .AsQueryable();

        if (FilterType.HasValue)
        {
            query = query.Where(r => r.Type == FilterType.Value);
        }

        if (FilterVisibility.HasValue)
        {
            query = query.Where(r => r.Visibility == FilterVisibility.Value);
        }

        if (!string.IsNullOrWhiteSpace(Search))
        {
            var search = $"%{Search.Trim()}%";
            query = query.Where(r => EF.Functions.ILike(r.Title, search));
        }

        TotalRecipes = await query.CountAsync();

        // Ensure page is within valid range
        if (CurrentPage > TotalPages && TotalPages > 0)
        {
            CurrentPage = TotalPages;
        }

        Recipes = await query
            .OrderByDescending(r => r.CreatedAt)
            .ThenBy(r => r.Id)
            .Skip((CurrentPage - 1) * PageSize)
            .Take(PageSize)
            .Select(r => new RecipeListItem
            {
                Id = r.Id,
                Title = r.Title,
                Description = r.Description,
                Type = r.Type,
                Visibility = r.Visibility,
                AuthorNickname = r.Author != null ? r.Author.Nickname : null,
                LikesCount = r.LikesCount,
                CommentsCount = r.CommentsCount,
                SavedCount = r.SavedCount,
                Difficulty = r.Difficulty,
                IsFeatured = r.IsFeatured,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            })
            .ToListAsync();
    }

    public class RecipeListItem
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public RecipeType Type { get; set; }
        public RecipeVisibility Visibility { get; set; }
        public string? AuthorNickname { get; set; }
        public int LikesCount { get; set; }
        public int CommentsCount { get; set; }
        public int SavedCount { get; set; }
        public RecipeDifficulty Difficulty { get; set; }
        public bool IsFeatured { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class EditRecipeModel
    {
        [Required]
        [MaxLength(256)]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required]
        public RecipeType Type { get; set; }

        [Required]
        public RecipeVisibility Visibility { get; set; }

        public RecipeDifficulty Difficulty { get; set; }
    }
}
