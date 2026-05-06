using System.ComponentModel.DataAnnotations;

namespace backend.Dtos.Tags;

public record TagTypeResponseDto(
    int Id,
    string Name,
    string DisplayName,
    string? Description,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public class CreateTagTypeRequestDto
{
    [Required]
    [MaxLength(64)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(512)]
    public string? Description { get; set; }
}

public class UpdateTagTypeRequestDto
{
    [Required]
    [MaxLength(64)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(512)]
    public string? Description { get; set; }
}
