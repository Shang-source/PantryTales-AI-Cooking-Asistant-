using backend.Models;

namespace backend.Interfaces;

public interface ITagRepository
{
    Task<List<Tag>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Tag?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Tag> AddAsync(Tag tag, CancellationToken cancellationToken = default);
    Task UpdateAsync(Tag tag, CancellationToken cancellationToken = default);
    Task DeleteAsync(Tag tag, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string name, string type, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsyncExcludingIdAsync(string name, string type, int excludeId, CancellationToken cancellationToken = default);

    // Tag Types
    Task<List<TagType>> GetTagTypesAsync(CancellationToken cancellationToken = default);
    Task<TagType?> GetTagTypeByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<TagType> AddTagTypeAsync(TagType tagType, CancellationToken cancellationToken = default);
    Task UpdateTagTypeAsync(TagType tagType, CancellationToken cancellationToken = default);
    Task DeleteTagTypeAsync(TagType tagType, CancellationToken cancellationToken = default);
    Task<bool> TagTypeExistsAsync(string name, CancellationToken cancellationToken = default);
    Task<bool> HasTagsOfTypeAsync(string typeName, CancellationToken cancellationToken = default);
}
