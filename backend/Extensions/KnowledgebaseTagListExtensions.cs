using backend.Dtos.Tags;
using backend.Models;

namespace backend.Extensions;

public static class KnowledgebaseTagListExtensions
{
    public static TagResponseDto ToTagListResponseDto(this Tag tag) =>
        new(
            tag.Id,
            tag.Name,
            tag.DisplayName,
            tag.Type,
            tag.Icon,
            tag.Color,
            tag.IsActive,
            tag.CreatedAt,
            tag.UpdatedAt);
}
