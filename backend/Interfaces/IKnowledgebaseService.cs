using backend.Dtos;
using backend.Dtos.Knowledgebase;
using backend.Dtos.Tags;

namespace backend.Interfaces;

public interface IKnowledgebaseService
{
    Task<PagedResponse<KnowledgebaseArticleListDto>> GetPublishedByTagAsync(
        int tagId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<bool> TagExistsAsync(int tagId, CancellationToken cancellationToken = default);
    Task<KnowledgebaseArticleDetailDto?> GetArticleByIdAsync(Guid articleId, CancellationToken cancellationToken = default);
    Task<List<KnowledgebaseArticleListDto>> SearchPublishedArticlesAsync(
        string keyword,
        CancellationToken cancellationToken = default);
    Task<List<TagResponseDto>> ListTagsForArticlesAsync(CancellationToken cancellationToken = default);
    Task<KnowledgebaseArticleDetailDto> CreateArticleAsync(
        CreateArticleRequestDto request,
        CancellationToken cancellationToken = default);
    Task<List<KnowledgebaseArticleListDto>> GetFeaturedArticlesAsync(
        int count,
        CancellationToken cancellationToken = default);
}
