using backend.Dtos.Knowledgebase;
using backend.Models;

namespace backend.Extensions;

public static class KnowledgebaseArticleListExtensions
{
    public static KnowledgebaseArticleListDto ToArticleListDto(this KnowledgebaseArticle article) =>
        new(
            article.Id,
            article.TagId,
            article.Title,
            article.Subtitle,
            article.IconName,
            article.CreatedAt,
            article.UpdatedAt);
}
