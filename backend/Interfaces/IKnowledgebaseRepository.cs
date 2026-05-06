using backend.Models;

namespace backend.Interfaces;

public interface IKnowledgebaseRepository
{
    Task<(List<KnowledgebaseArticle> Items, int TotalCount)> ListPublishedByTagAsync(int tagId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<bool> TagExistsAsync(int tagId, CancellationToken cancellationToken = default);
    Task<KnowledgebaseArticle?> GetArticleByIdAsync(Guid articleId, CancellationToken cancellationToken = default);
    Task<List<KnowledgebaseArticle>> SearchPublishedArticlesAsync(
        string keyword,
        CancellationToken cancellationToken = default);
    Task<List<Tag>> ListTagsForArticlesAsync(CancellationToken cancellationToken = default);
    Task<KnowledgebaseArticle> CreateArticleAsync(KnowledgebaseArticle article, CancellationToken cancellationToken = default);
    Task<List<KnowledgebaseArticle>> GetAllArticlesAsync(int? tagId = null, CancellationToken cancellationToken = default);
    Task<(List<KnowledgebaseArticle> Items, int TotalCount)> GetAllArticlesPagedAsync(int? tagId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<KnowledgebaseArticle?> GetArticleByIdForEditAsync(Guid articleId, CancellationToken cancellationToken = default);
    Task UpdateArticleAsync(KnowledgebaseArticle article, CancellationToken cancellationToken = default);
    Task DeleteArticleAsync(KnowledgebaseArticle article, CancellationToken cancellationToken = default);
    Task<List<KnowledgebaseArticle>> GetFeaturedArticlesAsync(int count, CancellationToken cancellationToken = default);
    Task ToggleFeaturedAsync(Guid articleId, CancellationToken cancellationToken = default);
}
