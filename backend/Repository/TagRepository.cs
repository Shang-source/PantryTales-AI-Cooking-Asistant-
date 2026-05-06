using backend.Data;
using backend.Interfaces;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Repository;

public class TagRepository(AppDbContext context, ILogger<TagRepository> logger) : ITagRepository
{
    // Logger kept for potential future use
    private readonly ILogger<TagRepository> _ = logger;

    public async Task<List<Tag>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await context.Tags
            .OrderBy(t => t.Type)
            .ThenBy(t => t.DisplayName)
            .ToListAsync(cancellationToken);
    }

    public async Task<Tag?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await context.Tags.FindAsync([id], cancellationToken);
    }

    public async Task<Tag> AddAsync(Tag tag, CancellationToken cancellationToken = default)
    {
        context.Tags.Add(tag);
        await context.SaveChangesAsync(cancellationToken);
        return tag;
    }

    public async Task UpdateAsync(Tag tag, CancellationToken cancellationToken = default)
    {
        tag.UpdatedAt = DateTime.UtcNow;
        context.Tags.Update(tag);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Tag tag, CancellationToken cancellationToken = default)
    {
        context.Tags.Remove(tag);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(string name, string type, CancellationToken cancellationToken = default)
    {
        return await context.Tags.AnyAsync(t => t.Name == name && t.Type == type, cancellationToken);
    }

    public async Task<bool> ExistsAsyncExcludingIdAsync(string name, string type, int excludeId, CancellationToken cancellationToken = default)
    {
        return await context.Tags.AnyAsync(t => t.Name == name && t.Type == type && t.Id != excludeId, cancellationToken);
    }

    public async Task<List<TagType>> GetTagTypesAsync(CancellationToken cancellationToken = default)
    {
        return await context.TagTypes
            .AsNoTracking()
            .OrderBy(tt => tt.DisplayName)
            .ToListAsync(cancellationToken);
    }

    public async Task<TagType?> GetTagTypeByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await context.TagTypes.FindAsync([id], cancellationToken);
    }

    public async Task<TagType> AddTagTypeAsync(TagType tagType, CancellationToken cancellationToken = default)
    {
        // CreatedAt is init-only and automatically set to DateTime.UtcNow
        tagType.UpdatedAt = DateTime.UtcNow;
        context.TagTypes.Add(tagType);
        await context.SaveChangesAsync(cancellationToken);
        return tagType;
    }

    public async Task UpdateTagTypeAsync(TagType tagType, CancellationToken cancellationToken = default)
    {
        tagType.UpdatedAt = DateTime.UtcNow;
        context.TagTypes.Update(tagType);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteTagTypeAsync(TagType tagType, CancellationToken cancellationToken = default)
    {
        context.TagTypes.Remove(tagType);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> TagTypeExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        return await context.TagTypes.AnyAsync(tt => tt.Name == name, cancellationToken);
    }

    public async Task<bool> HasTagsOfTypeAsync(string typeName, CancellationToken cancellationToken = default)
    {
        return await context.Tags.AnyAsync(t => t.Type == typeName, cancellationToken);
    }
}
