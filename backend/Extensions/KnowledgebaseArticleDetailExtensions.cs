using backend.Dtos.Knowledgebase;
using backend.Models;

namespace backend.Extensions;

public static class KnowledgebaseArticleDetailExtensions
{
    public static KnowledgebaseArticleDetailDto ToArticleDetailDto(this KnowledgebaseArticle article) =>
        new(
            article.Id,
            article.TagId,
            article.Title,
            article.Subtitle,
            article.IconName,
            article.Content,
            article.IsPublished,
            article.CreatedAt,
            article.UpdatedAt
        );
}
