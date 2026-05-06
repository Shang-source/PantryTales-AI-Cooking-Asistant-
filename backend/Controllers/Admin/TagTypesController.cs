using backend.Data;
using backend.Dtos.Tags;
using backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers.Admin;

[ApiController]
[Route("api/admin/tag-types")]
[Authorize]
public class TagTypesController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TagTypeResponseDto>>> ListAsync(CancellationToken cancellationToken)
    {
        var items = await dbContext.TagTypes
            .OrderBy(t => t.DisplayName)
            .Select(t => ToDto(t))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TagTypeResponseDto>> GetAsync(int id, CancellationToken cancellationToken)
    {
        var tagType = await dbContext.TagTypes.FindAsync([id], cancellationToken);
        if (tagType is null)
        {
            return NotFound();
        }

        return Ok(ToDto(tagType));
    }

    [HttpPost]
    public async Task<ActionResult<TagTypeResponseDto>> CreateAsync(
        [FromBody] CreateTagTypeRequestDto request,
        CancellationToken cancellationToken)
    {
        var normalizedName = NormalizeName(request.Name);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            ModelState.AddModelError(nameof(request.Name), "Name cannot be empty.");
        }

        var displayName = request.DisplayName.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            ModelState.AddModelError(nameof(request.DisplayName), "DisplayName cannot be empty.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem();
        }

        var exists = await dbContext.TagTypes.AnyAsync(t => t.Name == normalizedName, cancellationToken);
        if (exists)
        {
            return Conflict($"Tag type '{normalizedName}' already exists.");
        }

        var tagType = new TagType
        {
            Name = normalizedName,
            DisplayName = displayName,
            Description = request.Description?.Trim()
        };

        dbContext.TagTypes.Add(tagType);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction("Get", new { id = tagType.Id }, ToDto(tagType));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<TagTypeResponseDto>> UpdateAsync(
        int id,
        [FromBody] UpdateTagTypeRequestDto request,
        CancellationToken cancellationToken)
    {
        var tagType = await dbContext.TagTypes.FindAsync([id], cancellationToken);
        if (tagType is null)
        {
            return NotFound();
        }

        var normalizedName = NormalizeName(request.Name);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            ModelState.AddModelError(nameof(request.Name), "Name cannot be empty.");
        }

        var displayName = request.DisplayName.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            ModelState.AddModelError(nameof(request.DisplayName), "DisplayName cannot be empty.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem();
        }

        var duplicate = await dbContext.TagTypes
            .AnyAsync(t => t.Id != id && t.Name == normalizedName, cancellationToken);
        if (duplicate)
        {
            return Conflict($"Tag type '{normalizedName}' already exists.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var originalName = tagType.Name;
        var renamed = !string.Equals(originalName, normalizedName, StringComparison.Ordinal);
        tagType.Name = normalizedName;
        tagType.DisplayName = displayName;
        tagType.Description = request.Description?.Trim();
        tagType.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        if (renamed)
        {
            await dbContext.Tags
                .Where(t => t.Type == originalName)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(t => t.Type, normalizedName)
                        .SetProperty(t => t.UpdatedAt, DateTime.UtcNow),
                    cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return Ok(ToDto(tagType));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var tagType = await dbContext.TagTypes.FindAsync([id], cancellationToken);
        if (tagType is null)
        {
            return NotFound();
        }

        var hasTags = await dbContext.Tags.AnyAsync(t => t.Type == tagType.Name, cancellationToken);
        if (hasTags)
        {
            return Conflict("Cannot delete tag type while tags are associated with it.");
        }

        dbContext.TagTypes.Remove(tagType);
        await dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private static string NormalizeName(string value) => value.Trim().ToLowerInvariant();

    private static TagTypeResponseDto ToDto(TagType tagType) =>
        new(tagType.Id, tagType.Name, tagType.DisplayName, tagType.Description, tagType.CreatedAt, tagType.UpdatedAt);
}
