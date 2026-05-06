using backend.Data;
using backend.Interfaces;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Repository;

public class KnowledgebaseRepository(AppDbContext context)
    : IKnowledgebaseRepository
{
    public async Task<(List<KnowledgebaseArticle> Items, int TotalCount)> ListPublishedByTagAsync(
        int tagId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = context.KnowledgebaseArticles
            .Where(a => a.TagId == tagId && a.IsPublished);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.UpdatedAt)
            .ThenBy(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
    public async Task<bool> TagExistsAsync(int tagId, CancellationToken cancellationToken = default)
    {
        return await context.Tags.AnyAsync(t => t.Id == tagId, cancellationToken);
    }
    public async Task<KnowledgebaseArticle?> GetArticleByIdAsync(Guid articleId, CancellationToken cancellationToken = default)
    {
        return await context.KnowledgebaseArticles
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == articleId && a.IsPublished, cancellationToken);
    }

    public async Task<List<KnowledgebaseArticle>> SearchPublishedArticlesAsync(
        string keyword,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return [];
        }

        var search = $"%{keyword}%";

        return await context.KnowledgebaseArticles
            .Include(a => a.Tag)
            .AsNoTracking()
            .Where(a => a.IsPublished &&
                        (EF.Functions.ILike(a.Title, search) ||
                         (a.Subtitle != null && EF.Functions.ILike(a.Subtitle, search)) ||
                         EF.Functions.ILike(a.Content, search) ||
                         EF.Functions.ILike(a.Tag.DisplayName, search) ||
                         EF.Functions.ILike(a.Tag.Name, search)))
            .OrderByDescending(a => a.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Tag>> ListTagsForArticlesAsync(CancellationToken cancellationToken = default)
    {
        var tagIdsForPublishedArticles = context.KnowledgebaseArticles
            .Where(a => a.IsPublished)
            .Select(a => a.TagId)
            .Distinct();

        var allTags = await context.Tags
            .AsNoTracking()
            .Where(t => t.IsActive && tagIdsForPublishedArticles.Contains(t.Id))
            .ToListAsync(cancellationToken);

        // Custom sort to prioritize food-related tags
        return allTags
            .OrderBy(t => GetTagPriority(t.DisplayName ?? t.Name))
            .ThenBy(t => t.DisplayName)
            .ToList();
    }

    private const int PriorityRecipe = 1;
    private const int PriorityCooking = 2;
    private const int PriorityFood = 3;
    private const int PriorityDiet = 4;
    private const int PriorityIngredient = 5;
    private const int PriorityDefault = 50;
    private const int PriorityScienceDeprioritized = 99;
    private static int GetTagPriority(string tagName)
    {
        var normalized = tagName.ToLower();
        if (normalized.Contains("recipe")) return PriorityRecipe;
        if (normalized.Contains("cook") || normalized.Contains("chef")) return PriorityCooking;
        if (normalized.Contains("chem") || normalized.Contains("science")) return PriorityScienceDeprioritized; // Deprioritize science explicitly
        if (normalized.Contains("food") || normalized.Contains("eat")) return PriorityFood;
        if (normalized.Contains("diet") || normalized.Contains("nutri")) return PriorityDiet;
        if (normalized.Contains("ingredient")) return PriorityIngredient;
        return PriorityDefault; // Default priority
    }

    public async Task<KnowledgebaseArticle> CreateArticleAsync(KnowledgebaseArticle article, CancellationToken cancellationToken = default)
    {
        context.KnowledgebaseArticles.Add(article);
        await context.SaveChangesAsync(cancellationToken);
        return article;
    }

    /// <summary>
    /// Get all articles, optionally filtered by tag. Uses AsNoTracking for read-only performance.
    /// </summary>
    public async Task<List<KnowledgebaseArticle>> GetAllArticlesAsync(int? tagId = null, CancellationToken cancellationToken = default)
    {
        var query = context.KnowledgebaseArticles
            .Include(a => a.Tag)
            .AsNoTracking();

        if (tagId.HasValue)
        {
            query = query.Where(a => a.TagId == tagId.Value);
        }

        return await query
            .OrderByDescending(a => a.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Get all articles with pagination, optionally filtered by tag.
    /// </summary>
    public async Task<(List<KnowledgebaseArticle> Items, int TotalCount)> GetAllArticlesPagedAsync(
        int? tagId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = context.KnowledgebaseArticles
            .Include(a => a.Tag)
            .AsNoTracking();

        if (tagId.HasValue)
        {
            query = query.Where(a => a.TagId == tagId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.UpdatedAt)
            .ThenBy(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    /// <summary>
    /// Get article by ID for editing. Uses tracking to enable change detection for updates.
    /// </summary>
    public async Task<KnowledgebaseArticle?> GetArticleByIdForEditAsync(Guid articleId, CancellationToken cancellationToken = default)
    {
        return await context.KnowledgebaseArticles
            .FirstOrDefaultAsync(a => a.Id == articleId, cancellationToken);
    }

    public async Task UpdateArticleAsync(KnowledgebaseArticle article, CancellationToken cancellationToken = default)
    {
        context.KnowledgebaseArticles.Update(article);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteArticleAsync(KnowledgebaseArticle article, CancellationToken cancellationToken = default)
    {
        context.KnowledgebaseArticles.Remove(article);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Get featured articles for the homepage ticker.
    /// Returns published articles where IsFeatured is true, ordered randomly.
    /// </summary>
    public async Task<List<KnowledgebaseArticle>> GetFeaturedArticlesAsync(int count, CancellationToken cancellationToken = default)
    {
        return await context.KnowledgebaseArticles
            .Include(a => a.Tag)
            .AsNoTracking()
            .Where(a => a.IsPublished && a.IsFeatured)
            .OrderBy(_ => EF.Functions.Random())
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Toggle the featured status of an article.
    /// </summary>
    public async Task ToggleFeaturedAsync(Guid articleId, CancellationToken cancellationToken = default)
    {
        var article = await context.KnowledgebaseArticles
            .FirstOrDefaultAsync(a => a.Id == articleId, cancellationToken);

        if (article is not null)
        {
            article.IsFeatured = !article.IsFeatured;
            article.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
