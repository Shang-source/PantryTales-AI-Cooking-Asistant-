using backend.Dtos;
using backend.Dtos.Knowledgebase;
using backend.Dtos.Tags;
using backend.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/knowledgebase")]
[Authorize]
public class KnowledgebaseController(
    IKnowledgebaseService knowledgebaseService,
    ILogger<KnowledgebaseController> logger) : ControllerBase
{
    private const int MaxSearchKeywordLength = 256;

    [HttpGet("taglist")]
    public async Task<ActionResult<ApiResponse<List<TagResponseDto>>>> ListTagsForKnowledgebaseAsync(
        CancellationToken cancellationToken)
    {
        var tags = await knowledgebaseService.ListTagsForArticlesAsync(cancellationToken);
        if (tags.Count == 0)
        {
            logger.LogInformation("No published knowledgebase article tags found.");
            return Ok(ApiResponse<List<TagResponseDto>>.Success(
                tags,
                message: "No published knowledgebase article tags."));
        }
        return Ok(ApiResponse<List<TagResponseDto>>.Success(tags));
    }

    [HttpGet("articlelist/{tagId:int}")]
    public async Task<ActionResult<ApiResponse<PagedResponse<KnowledgebaseArticleListDto>>>> GetPublishedByTagAsync(
        [FromRoute] int tagId, 
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100)
        {
            return BadRequest(ApiResponse<PagedResponse<KnowledgebaseArticleListDto>>.Fail(400, "Page must be greater than 0, and PageSize must be between 1 and 100."));
        }

        var tagExists = await knowledgebaseService.TagExistsAsync(tagId, cancellationToken);
        if (!tagExists)
        {
            logger.LogWarning("Tag {TagId} not found when fetching knowledgebase articles.", tagId);
            return NotFound(ApiResponse<PagedResponse<KnowledgebaseArticleListDto>>.Fail(404, "Tag not found."));
        }

        var pagedResult = await knowledgebaseService.GetPublishedByTagAsync(tagId, page, pageSize, cancellationToken);
        if (pagedResult.TotalCount == 0)
        {
            logger.LogInformation("No published knowledgebase articles found for tag {TagId}.", tagId);
            return Ok(ApiResponse<PagedResponse<KnowledgebaseArticleListDto>>.Success(
                pagedResult,
                message: "No published articles for this tag."));
        }

        logger.LogInformation("Found {Count} published knowledgebase articles for tag {TagId} (Page {Page}).", pagedResult.Items.Count(), tagId, page);
        return Ok(ApiResponse<PagedResponse<KnowledgebaseArticleListDto>>.Success(pagedResult));
    }

    [HttpGet("article/{articleId:guid}", Name = "GetKnowledgebaseArticleById")]
    public async Task<ActionResult<ApiResponse<KnowledgebaseArticleDetailDto>>> GetPublishedByIdAsync(
    [FromRoute] Guid articleId, CancellationToken cancellationToken)
    {
        var article = await knowledgebaseService.GetArticleByIdAsync(articleId, cancellationToken);
        if (article is null)
        {
            logger.LogWarning("Knowledgebase article {ArticleId} not found or not published.", articleId);
            return NotFound(ApiResponse<KnowledgebaseArticleDetailDto>.Fail(404, "Article not found or not published."));
        }

        logger.LogInformation("Found knowledgebase article {ArticleId}.", articleId);
        return Ok(ApiResponse<KnowledgebaseArticleDetailDto>.Success(article));
    }

    [HttpGet("articles/search")]
    public async Task<ActionResult<ApiResponse<List<KnowledgebaseArticleListDto>>>> SearchPublishedArticlesAsync(
        [FromQuery] string keyword,
        CancellationToken cancellationToken)
    {
        var trimmedKeyword = keyword?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedKeyword))
        {
            return BadRequest(ApiResponse<List<KnowledgebaseArticleListDto>>.Fail(400, "Keyword is required."));
        }

        if (trimmedKeyword.Length > MaxSearchKeywordLength)
        {
            return BadRequest(ApiResponse<List<KnowledgebaseArticleListDto>>.Fail(
                400,
                $"Keyword is too long. Maximum length is {MaxSearchKeywordLength} characters."));
        }

        var articles = await knowledgebaseService.SearchPublishedArticlesAsync(trimmedKeyword, cancellationToken);
        if (articles.Count == 0)
        {
            logger.LogInformation("No published knowledgebase articles found for keyword '{Keyword}'.", trimmedKeyword);
            return Ok(ApiResponse<List<KnowledgebaseArticleListDto>>.Success(
                articles,
                message: "No articles found for this keyword."));
        }

        logger.LogInformation("Found {Count} knowledgebase articles for keyword '{Keyword}'.", articles.Count,
            trimmedKeyword);
        return Ok(ApiResponse<List<KnowledgebaseArticleListDto>>.Success(articles));
    }

    /// <summary>
    /// Get featured articles for the homepage ticker.
    /// </summary>
    /// <param name="count">Number of articles to return (default 10, max 50).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("featured")]
    public async Task<ActionResult<ApiResponse<List<KnowledgebaseArticleListDto>>>> GetFeaturedArticlesAsync(
        [FromQuery] int count = 10,
        CancellationToken cancellationToken = default)
    {
        if (count < 1 || count > 50)
        {
            return BadRequest(ApiResponse<List<KnowledgebaseArticleListDto>>.Fail(400, "Count must be between 1 and 50."));
        }

        var articles = await knowledgebaseService.GetFeaturedArticlesAsync(count, cancellationToken);
        if (articles.Count == 0)
        {
            logger.LogInformation("No featured knowledgebase articles found.");
            return Ok(ApiResponse<List<KnowledgebaseArticleListDto>>.Success(
                articles,
                message: "No featured articles available."));
        }

        logger.LogInformation("Found {Count} featured knowledgebase articles.", articles.Count);
        return Ok(ApiResponse<List<KnowledgebaseArticleListDto>>.Success(articles));
    }
}
