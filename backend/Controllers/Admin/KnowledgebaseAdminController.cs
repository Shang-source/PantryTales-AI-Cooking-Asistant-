using backend.Dtos;
using backend.Dtos.Knowledgebase;
using backend.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers.Admin;

[ApiController]
[Route("api/admin/knowledgebase")]
[Authorize]
public class KnowledgebaseAdminController(
    IKnowledgebaseService knowledgebaseService,
    ILogger<KnowledgebaseAdminController> logger) : ControllerBase
{
    [HttpPost("article")]
    public async Task<ActionResult<ApiResponse<KnowledgebaseArticleDetailDto>>> CreateArticleAsync(
        [FromBody] CreateArticleRequestDto request,
        CancellationToken cancellationToken)
    {
        var tagExists = await knowledgebaseService.TagExistsAsync(request.TagId, cancellationToken);
        if (!tagExists)
        {
            logger.LogWarning("Tag {TagId} not found when creating knowledgebase article.", request.TagId);
            return NotFound(ApiResponse<KnowledgebaseArticleDetailDto>.Fail(404, "Tag not found."));
        }

        var created = await knowledgebaseService.CreateArticleAsync(request, cancellationToken);

        logger.LogInformation("Knowledgebase article {ArticleId} created.", created.Id);

        return CreatedAtRoute(
            "GetKnowledgebaseArticleById",
            new { articleId = created.Id },
            ApiResponse<KnowledgebaseArticleDetailDto>.Success(created, code: 201, message: "Created"));
    }
}
