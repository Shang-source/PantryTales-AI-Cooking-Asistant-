using System.ComponentModel.DataAnnotations;
using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Pages.Admin.IngredientDictionary;

public class IndexModel(AppDbContext dbContext) : AdminPageModel
{
    private const int MaxPageSize = 100;

    public List<IngredientViewModel> Ingredients { get; set; } = [];
    public List<Tag> AvailableTags { get; set; } = [];
    public List<TagType> TagTypes { get; set; } = [];
    public int TotalIngredients { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalIngredients / (double)PageSize);

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? FilterTagId { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool ShowUntagged { get; set; } = false;

    [BindProperty(SupportsGet = true, Name = "p")]
    [Range(1, int.MaxValue)]
    public int CurrentPage { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    [Range(1, MaxPageSize)]
    public int PageSize { get; set; } = 20;

    // For adding tag action
    [BindProperty]
    public Guid AddTagIngredientId { get; set; }

    [BindProperty]
    public int AddTagId { get; set; }

    // For removing tag action
    [BindProperty]
    public Guid RemoveTagIngredientId { get; set; }

    [BindProperty]
    public int RemoveTagId { get; set; }

    public async Task OnGetAsync()
    {
        PageSize = Math.Clamp(PageSize, 1, MaxPageSize);
        await LoadDataAsync();
    }

    public async Task<IActionResult> OnPostAddTagAsync()
    {
        if (AddTagIngredientId == Guid.Empty || AddTagId == 0)
        {
            TempData["Error"] = "Invalid ingredient or tag.";
            return RedirectToPage(new { p = CurrentPage, PageSize, Search, FilterTagId, ShowUntagged });
        }

        var ingredient = await dbContext.Ingredients.FindAsync(AddTagIngredientId);
        if (ingredient == null)
        {
            TempData["Error"] = "Ingredient not found.";
            return RedirectToPage(new { p = CurrentPage, PageSize, Search, FilterTagId, ShowUntagged });
        }

        var tag = await dbContext.Tags.FindAsync(AddTagId);
        if (tag == null)
        {
            TempData["Error"] = "Tag not found.";
            return RedirectToPage(new { p = CurrentPage, PageSize, Search, FilterTagId, ShowUntagged });
        }

        // Check if already exists
        var exists = await dbContext.IngredientTags
            .AnyAsync(it => it.IngredientId == AddTagIngredientId && it.TagId == AddTagId);

        if (exists)
        {
            TempData["Error"] = $"Tag '{tag.DisplayName}' is already assigned to '{ingredient.CanonicalName}'.";
            return RedirectToPage(new { p = CurrentPage, PageSize, Search, FilterTagId, ShowUntagged });
        }

        dbContext.IngredientTags.Add(new IngredientTag
        {
            IngredientId = AddTagIngredientId,
            TagId = AddTagId
        });

        await dbContext.SaveChangesAsync();
        TempData["Success"] = $"Added tag '{tag.DisplayName}' to '{ingredient.CanonicalName}'.";
        return RedirectToPage(new { p = CurrentPage, PageSize, Search, FilterTagId, ShowUntagged });
    }

    public async Task<IActionResult> OnPostRemoveTagAsync()
    {
        if (RemoveTagIngredientId == Guid.Empty || RemoveTagId == 0)
        {
            TempData["Error"] = "Invalid ingredient or tag.";
            return RedirectToPage(new { p = CurrentPage, PageSize, Search, FilterTagId, ShowUntagged });
        }

        var ingredientTag = await dbContext.IngredientTags
            .FirstOrDefaultAsync(it => it.IngredientId == RemoveTagIngredientId && it.TagId == RemoveTagId);

        if (ingredientTag == null)
        {
            TempData["Error"] = "Tag mapping not found.";
            return RedirectToPage(new { p = CurrentPage, PageSize, Search, FilterTagId, ShowUntagged });
        }

        var ingredient = await dbContext.Ingredients.FindAsync(RemoveTagIngredientId);
        var tag = await dbContext.Tags.FindAsync(RemoveTagId);

        dbContext.IngredientTags.Remove(ingredientTag);
        await dbContext.SaveChangesAsync();

        TempData["Success"] = $"Removed tag '{tag?.DisplayName ?? "Unknown"}' from '{ingredient?.CanonicalName ?? "Unknown"}'.";
        return RedirectToPage(new { p = CurrentPage, PageSize, Search, FilterTagId, ShowUntagged });
    }

    private async Task LoadDataAsync()
    {
        // Load available tags (ingredient type or all active tags)
        AvailableTags = await dbContext.Tags
            .Where(t => t.IsActive)
            .OrderBy(t => t.Type)
            .ThenBy(t => t.DisplayName)
            .ToListAsync();

        // Load tag types for grouping
        TagTypes = await dbContext.TagTypes
            .OrderBy(t => t.DisplayName)
            .ToListAsync();

        // Build ingredient query
        var query = dbContext.Ingredients
            .AsNoTracking()
            .Include(i => i.Tags)
            .ThenInclude(it => it.Tag)
            .AsQueryable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(Search))
        {
            var search = $"%{Search.Trim()}%";
            query = query.Where(i => EF.Functions.ILike(i.CanonicalName, search));
        }

        // Apply tag filter
        if (FilterTagId.HasValue)
        {
            query = query.Where(i => i.Tags.Any(t => t.TagId == FilterTagId.Value));
        }

        // Show only untagged
        if (ShowUntagged)
        {
            query = query.Where(i => !i.Tags.Any());
        }

        TotalIngredients = await query.CountAsync();

        // Ensure page is valid
        if (CurrentPage > TotalPages && TotalPages > 0)
        {
            CurrentPage = TotalPages;
        }

        var ingredients = await query
            .OrderBy(i => i.CanonicalName)
            .Skip((CurrentPage - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        // Map to view model
        Ingredients = ingredients.Select(i => new IngredientViewModel
        {
            Id = i.Id,
            CanonicalName = i.CanonicalName,
            DefaultUnit = i.DefaultUnit,
            RecipeCount = i.RecipeIngredients.Count,
            Tags = i.Tags.Select(t => new TagViewModel
            {
                Id = t.Tag.Id,
                Name = t.Tag.Name,
                DisplayName = t.Tag.DisplayName,
                Type = t.Tag.Type,
                Color = t.Tag.Color
            }).OrderBy(t => t.Type).ThenBy(t => t.DisplayName).ToList()
        }).ToList();

        // Get recipe counts separately for performance
        var ingredientIds = Ingredients.Select(i => i.Id).ToList();
        var recipeCounts = await dbContext.RecipeIngredients
            .Where(ri => ingredientIds.Contains(ri.IngredientId))
            .GroupBy(ri => ri.IngredientId)
            .Select(g => new { IngredientId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.IngredientId, x => x.Count);

        foreach (var ingredient in Ingredients)
        {
            ingredient.RecipeCount = recipeCounts.GetValueOrDefault(ingredient.Id, 0);
        }
    }

    public class IngredientViewModel
    {
        public Guid Id { get; set; }
        public string CanonicalName { get; set; } = string.Empty;
        public string? DefaultUnit { get; set; }
        public int RecipeCount { get; set; }
        public List<TagViewModel> Tags { get; set; } = [];
    }

    public class TagViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string? Color { get; set; }
    }
}
