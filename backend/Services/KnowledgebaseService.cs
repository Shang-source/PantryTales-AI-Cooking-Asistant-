using backend.Dtos;
using backend.Dtos.Knowledgebase;
using backend.Dtos.Tags;
using backend.Extensions;
using backend.Interfaces;
using backend.Models;

namespace backend.Services;

public class KnowledgebaseService(
    IKnowledgebaseRepository repository,
    ILogger<KnowledgebaseService> logger) : IKnowledgebaseService
{
    public Task<bool> TagExistsAsync(int tagId, CancellationToken cancellationToken = default) =>
    repository.TagExistsAsync(tagId, cancellationToken);
    public async Task<PagedResponse<KnowledgebaseArticleListDto>> GetPublishedByTagAsync(
        int tagId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await repository.ListPublishedByTagAsync(tagId, page, pageSize, cancellationToken);
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        logger.LogInformation("Found {Count} published KB articles for tag {TagId} (Page {Page}/{TotalPages})", items.Count, tagId, page, totalPages);

        var dtos = items.Select(a => a.ToArticleListDto()).ToList();

        return new PagedResponse<KnowledgebaseArticleListDto>
        {
            Items = dtos,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        };
    }
    public async Task<KnowledgebaseArticleDetailDto?> GetArticleByIdAsync(Guid articleId, CancellationToken cancellationToken = default)
    {
        var article = await repository.GetArticleByIdAsync(articleId, cancellationToken);
        if (article is null)
        {
            logger.LogInformation("KB article {ArticleId} not found or not published", articleId);
            return null;
        }

        return article.ToArticleDetailDto();
    }

    public async Task<List<KnowledgebaseArticleListDto>> SearchPublishedArticlesAsync(
        string keyword,
        CancellationToken cancellationToken = default)
    {
        var trimmedKeyword = keyword.Trim();
        if (string.IsNullOrWhiteSpace(trimmedKeyword))
        {
            return [];
        }

        var articles = await repository.SearchPublishedArticlesAsync(trimmedKeyword, cancellationToken);

        logger.LogInformation("Found {Count} published KB articles matching keyword '{Keyword}'", articles.Count,
            trimmedKeyword);

        return articles
            .Select(a => a.ToArticleListDto())
            .ToList();
    }

    public async Task<List<TagResponseDto>> ListTagsForArticlesAsync(
        CancellationToken cancellationToken = default)
    {
        var tags = await repository.ListTagsForArticlesAsync(cancellationToken);

        logger.LogInformation("Found {Count} knowledgebase tags for published articles", tags.Count);

        return tags
            .Select(t => t.ToTagListResponseDto())
            .ToList();
    }

    public async Task<KnowledgebaseArticleDetailDto> CreateArticleAsync(
        CreateArticleRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var article = new KnowledgebaseArticle
        {
            TagId = request.TagId,
            Title = request.Title.Trim(),
            Subtitle = request.Subtitle?.Trim(),
            IconName = request.IconName?.Trim(),
            Content = request.Content,
            IsPublished = request.IsPublished,
            CreatedAt = now,
            UpdatedAt = now
        };

        var created = await repository.CreateArticleAsync(article, cancellationToken);

        logger.LogInformation("Created knowledgebase article {ArticleId} for tag {TagId}", created.Id, created.TagId);

        return created.ToArticleDetailDto();
    }

    public async Task<List<KnowledgebaseArticleListDto>> GetFeaturedArticlesAsync(
        int count,
        CancellationToken cancellationToken = default)
    {
        var articles = await repository.GetFeaturedArticlesAsync(count, cancellationToken);

        logger.LogInformation("Found {Count} featured KB articles for homepage ticker", articles.Count);

        return articles
            .Select(a => a.ToArticleListDto())
            .ToList();
    }
}
