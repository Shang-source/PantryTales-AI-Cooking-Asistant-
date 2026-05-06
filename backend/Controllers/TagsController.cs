using backend.Data;
using backend.Dtos.Tags;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using backend.Interfaces;

namespace backend.Controllers;

[ApiController]
[Route("api/tags")]
[AllowAnonymous]
public class TagsController(ITagRepository repository, ILogger<TagsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TagGroupDto>>> ListAsync(CancellationToken cancellationToken)
    {
        var allTags = await repository.GetAllAsync(cancellationToken);
        var tags = allTags.Where(t => t.IsActive).ToList();
        var tagTypes = await repository.GetTagTypesAsync(cancellationToken);

        var typeMap = tagTypes.ToDictionary(t => t.Name, t => t.DisplayName);

        var groups = tags
            .GroupBy(t => t.Type)
            .Select(g =>
            {
                var typeDisplayName = typeMap.GetValueOrDefault(g.Key) ?? g.Key;
                return new TagGroupDto(
                    g.Key,
                    typeDisplayName,
                    g.OrderBy(t => t.DisplayName)
                     .Select(t => new TagChipDto(t.Id, t.Name, t.DisplayName, t.Type, t.Color))
                     .ToList()
                );
            })
            .OrderBy(g => g.TypeDisplayName)
            .ToList();

        logger.LogInformation("TagsController.ListAsync returning {GroupCount} tag groups.", groups.Count);

        return Ok(groups);
    }
}
