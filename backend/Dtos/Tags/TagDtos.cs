using System.ComponentModel.DataAnnotations;

namespace backend.Dtos.Tags;

public record TagResponseDto(
    int Id,
    string Name,
    string DisplayName,
    string Type,
    string? Icon,
    string? Color,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record TagListResponseDto(int Total, IReadOnlyList<TagResponseDto> Items);

public class TagQueryParameters
{
    private const int MaxPageSize = 100;

    [MaxLength(64)]
    public string? Type { get; set; }

    [MaxLength(128)]
    public string? Search { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    private int _pageSize = 20;

    [Range(1, MaxPageSize)]
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = Math.Clamp(value, 1, MaxPageSize);
    }

    public bool IncludeInactive { get; set; }
}

public class TagRequestDtoBase
{
    [Required]
    [MaxLength(64)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    public string Type { get; set; } = string.Empty;

    [MaxLength(128)]
    public string? Icon { get; set; }

    [MaxLength(32)]
    public string? Color { get; set; }

    public bool IsActive { get; set; } = true;
}

public class CreateTagRequestDto : TagRequestDtoBase
{
}

public class UpdateTagRequestDto : TagRequestDtoBase
{
}

// Lightweight list DTOs for public tag consumption (e.g., preference chips).
public record TagChipDto(int Id, string Name, string DisplayName, string Type, string? Color = null);

public record TagGroupDto(string Type, string TypeDisplayName, IReadOnlyList<TagChipDto> Tags);
