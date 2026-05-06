using System.ComponentModel.DataAnnotations;
using backend.Interfaces;
using backend.Models;
using Microsoft.AspNetCore.Mvc;

namespace backend.Pages.Admin.Knowledgebase;

public class IndexModel(IKnowledgebaseRepository repository, ITagRepository tagRepository) : AdminPageModel
{
    private const int MaxPageSize = 100;

    public List<KnowledgebaseArticle> Articles { get; set; } = [];
    public List<Tag> Tags { get; set; } = [];
    public int TotalArticles { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalArticles / (double)PageSize);

    [BindProperty(SupportsGet = true)]
    public int? FilterTagId { get; set; }

    [BindProperty(SupportsGet = true, Name = "p")]
    [Range(1, int.MaxValue)]
    public int CurrentPage { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    [Range(1, MaxPageSize)]
    public int PageSize { get; set; } = 20;

    [BindProperty]
    public ArticleFormModel NewArticle { get; set; } = new() { IsPublished = true };

    [BindProperty]
    public Guid EditId { get; set; }

    [BindProperty]
    public ArticleFormModel EditArticle { get; set; } = new();

    public async Task OnGetAsync()
    {
        PageSize = Math.Clamp(PageSize, 1, MaxPageSize);
        await LoadDataAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadDataAsync();
            return Page();
        }

        var article = new KnowledgebaseArticle
        {
            Title = NewArticle.Title.Trim(),
            Subtitle = NewArticle.Subtitle?.Trim(),
            IconName = NewArticle.IconName?.Trim(),
            Content = NewArticle.Content.Trim(),
            TagId = NewArticle.TagId,
            IsPublished = NewArticle.IsPublished,
            IsFeatured = NewArticle.IsFeatured
        };

        await repository.CreateArticleAsync(article);
        TempData["Success"] = "Article created successfully.";
        return RedirectToPage("./Index", new { p = CurrentPage, PageSize, FilterTagId });
    }

    public async Task<IActionResult> OnPostEditAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadDataAsync();
            return Page();
        }

        var article = await repository.GetArticleByIdForEditAsync(EditId);
        if (article == null)
        {
            TempData["Error"] = "Article not found.";
            return RedirectToPage("./Index");
        }

        article.Title = EditArticle.Title.Trim();
        article.Subtitle = EditArticle.Subtitle?.Trim();
        article.IconName = EditArticle.IconName?.Trim();
        article.Content = EditArticle.Content.Trim();
        article.TagId = EditArticle.TagId;
        article.IsPublished = EditArticle.IsPublished;
        article.IsFeatured = EditArticle.IsFeatured;
        article.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateArticleAsync(article);
        TempData["Success"] = "Article updated successfully.";
        return RedirectToPage("./Index", new { p = CurrentPage, PageSize, FilterTagId });
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        var article = await repository.GetArticleByIdForEditAsync(id);
        if (article != null)
        {
            await repository.DeleteArticleAsync(article);
            TempData["Success"] = "Article deleted successfully.";
        }
        else
        {
            TempData["Error"] = "Article not found.";
        }
        return RedirectToPage("./Index", new { p = CurrentPage, PageSize, FilterTagId });
    }

    public async Task<IActionResult> OnPostToggleFeaturedAsync(Guid id)
    {
        await repository.ToggleFeaturedAsync(id);
        return RedirectToPage("./Index", new { p = CurrentPage, PageSize, FilterTagId });
    }

    private async Task LoadDataAsync()
    {
        // Database-level filtering with pagination for better performance
        var (items, totalCount) = await repository.GetAllArticlesPagedAsync(FilterTagId, CurrentPage, PageSize);
        Articles = items;
        TotalArticles = totalCount;
        Tags = await tagRepository.GetAllAsync();
    }

    /// <summary>
    /// Shared form model for create and edit operations.
    /// </summary>
    public class ArticleFormModel
    {
        [Required]
        [MaxLength(256)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(512)]
        public string? Subtitle { get; set; }

        [MaxLength(64)]
        public string? IconName { get; set; }

        [Required]
        public string Content { get; set; } = string.Empty;

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Please select a tag.")]
        public int TagId { get; set; }

        public bool IsPublished { get; set; }

        public bool IsFeatured { get; set; }
    }
}
