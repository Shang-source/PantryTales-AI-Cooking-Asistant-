using backend.Data;
using backend.Dtos.Tags;
using backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers.Admin;

[ApiController]
[Route("api/admin/tags")]
[Authorize]
public class TagsController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<TagListResponseDto>> ListAsync(
        [FromQuery] TagQueryParameters queryParameters,
        CancellationToken cancellationToken)
    {
        var tagsQuery = dbContext.Tags.AsQueryable();

        if (!queryParameters.IncludeInactive)
        {
            tagsQuery = tagsQuery.Where(t => t.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(queryParameters.Type))
        {
            var typeFilter = queryParameters.Type.Trim();
            tagsQuery = tagsQuery.Where(t => t.Type == typeFilter);
        }

        if (!string.IsNullOrWhiteSpace(queryParameters.Search))
        {
            var search = $"%{queryParameters.Search.Trim()}%";
            tagsQuery = tagsQuery.Where(t =>
                EF.Functions.ILike(t.Name, search) ||
                EF.Functions.ILike(t.DisplayName, search));
        }

        var total = await tagsQuery.CountAsync(cancellationToken);

        var items = await tagsQuery
            .OrderBy(t => t.DisplayName)
            .ThenBy(t => t.Id)
            .Skip((queryParameters.Page - 1) * queryParameters.PageSize)
            .Take(queryParameters.PageSize)
            .Select(t => ToDto(t))
            .ToListAsync(cancellationToken);

        return Ok(new TagListResponseDto(total, items));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TagResponseDto>> GetAsync(int id, CancellationToken cancellationToken)
    {
        var tag = await dbContext.Tags.FindAsync([id], cancellationToken);
        if (tag is null)
        {
            return NotFound();
        }

        return Ok(ToDto(tag));
    }

    [HttpPost]
    public async Task<ActionResult<TagResponseDto>> CreateAsync(
        [FromBody] CreateTagRequestDto request,
        CancellationToken cancellationToken)
    {
        var normalizedName = NormalizeName(request.Name);
        var normalizedType = NormalizeName(request.Type);
        var displayNameCandidate = request.DisplayName.Trim();

        if (string.IsNullOrWhiteSpace(normalizedName) || string.IsNullOrWhiteSpace(displayNameCandidate))
        {
            return ValidationProblem("Name and DisplayName are required.");
        }

        if (string.IsNullOrWhiteSpace(normalizedType))
        {
            return BadRequest("Type is required.");
        }

        var tagTypeExists =
            await dbContext.TagTypes.AnyAsync(t => t.Name == normalizedType, cancellationToken);
        if (!tagTypeExists)
        {
            return BadRequest($"Tag type '{normalizedType}' does not exist.");
        }

        var duplicate = await dbContext.Tags
            .AnyAsync(t => t.Name == normalizedName && t.Type == normalizedType, cancellationToken);
        if (duplicate)
        {
            return Conflict($"Tag '{normalizedName}' already exists for type '{normalizedType}'.");
        }

        var displayName = displayNameCandidate;

        var tag = new Tag
        {
            Name = normalizedName,
            DisplayName = displayName,
            Type = normalizedType,
            Icon = request.Icon?.Trim(),
            Color = request.Color?.Trim(),
            IsActive = request.IsActive
        };

        dbContext.Tags.Add(tag);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction("Get", new { id = tag.Id }, ToDto(tag));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<TagResponseDto>> UpdateAsync(
        int id,
        [FromBody] UpdateTagRequestDto request,
        CancellationToken cancellationToken)
    {
        var tag = await dbContext.Tags.FindAsync([id], cancellationToken);
        if (tag is null)
        {
            return NotFound();
        }

        var normalizedName = NormalizeName(request.Name);
        var normalizedType = NormalizeName(request.Type);
        var displayNameCandidate = request.DisplayName.Trim();

        if (string.IsNullOrWhiteSpace(normalizedName) || string.IsNullOrWhiteSpace(displayNameCandidate))
        {
            return ValidationProblem("Name and DisplayName are required.");
        }

        if (string.IsNullOrWhiteSpace(normalizedType))
        {
            return BadRequest("Type is required.");
        }

        var tagTypeExists =
            await dbContext.TagTypes.AnyAsync(t => t.Name == normalizedType, cancellationToken);
        if (!tagTypeExists)
        {
            return BadRequest($"Tag type '{normalizedType}' does not exist.");
        }

        var duplicate = await dbContext.Tags
            .AnyAsync(t => t.Id != id && t.Name == normalizedName && t.Type == normalizedType, cancellationToken);
        if (duplicate)
        {
            return Conflict($"Tag '{normalizedName}' already exists for type '{normalizedType}'.");
        }

        var displayName = displayNameCandidate;

        tag.Name = normalizedName;
        tag.DisplayName = displayName;
        tag.Type = normalizedType;
        tag.Icon = request.Icon?.Trim();
        tag.Color = request.Color?.Trim();
        tag.IsActive = request.IsActive;
        tag.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(tag));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var tag = await dbContext.Tags.FindAsync([id], cancellationToken);
        if (tag is null)
        {
            return NotFound();
        }

        // Soft delete: mark as inactive instead of removing
        tag.IsActive = false;
        tag.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private static string NormalizeName(string value) => value.Trim().ToLowerInvariant();

    private static TagResponseDto ToDto(Tag tag) =>
        new(tag.Id, tag.Name, tag.DisplayName, tag.Type, tag.Icon, tag.Color, tag.IsActive, tag.CreatedAt,
            tag.UpdatedAt);
}
