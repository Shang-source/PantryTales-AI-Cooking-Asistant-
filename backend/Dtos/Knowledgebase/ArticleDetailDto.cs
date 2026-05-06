namespace backend.Dtos.Knowledgebase;

public sealed record KnowledgebaseArticleDetailDto(
    Guid Id,
    int TagId,
    string Title,
    string? Subtitle,
    string? IconName,
    string Content,
    bool IsPublished,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);
