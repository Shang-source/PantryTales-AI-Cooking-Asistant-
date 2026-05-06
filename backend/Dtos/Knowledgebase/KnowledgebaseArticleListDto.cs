namespace backend.Dtos.Knowledgebase;

public sealed record KnowledgebaseArticleListDto(
    Guid Id,
    int TagId,
    string Title,
    string? Subtitle,
    string? IconName,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);
