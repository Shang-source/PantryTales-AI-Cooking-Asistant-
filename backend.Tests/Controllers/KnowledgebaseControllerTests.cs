using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using backend.Controllers;
using backend.Dtos;
using backend.Dtos.Knowledgebase;
using backend.Interfaces;
using backend.Dtos.Tags;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace backend.Tests.Controllers;

public class KnowledgebaseControllerTests
{
    [Fact]
    public async Task ListTagsForKnowledgebaseAsync_ReturnsOk_WithMessage_WhenNoTags()
    {
        var fakeService = new FakeKnowledgebaseService
        {
            Tags = new List<TagResponseDto>()
        };
        var logger = new TestLogger<KnowledgebaseController>();
        var controller = CreateController(fakeService, logger);

        var result = await controller.ListTagsForKnowledgebaseAsync(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<List<TagResponseDto>>>(ok.Value);
        Assert.Equal(0, api.Code);
        Assert.Equal("No published knowledgebase article tags.", api.Message);
        Assert.NotNull(api.Data);
        Assert.Empty(api.Data!);
        Assert.Contains(logger.Logs, log =>
            log.Level == LogLevel.Information &&
            log.Message.Contains("No published knowledgebase article tags found.", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ListTagsForKnowledgebaseAsync_ReturnsTags_WhenPublishedTagsExist()
    {
        var tag = new TagResponseDto(
            1,
            "tag-name",
            "Tag Name",
            "kb",
            "icon",
            "#fff",
            true,
            DateTime.Parse("2025-01-01T00:00:00Z"),
            DateTime.Parse("2025-01-02T00:00:00Z"));

        var fakeService = new FakeKnowledgebaseService
        {
            Tags = new List<TagResponseDto> { tag }
        };
        var logger = new TestLogger<KnowledgebaseController>();
        var controller = CreateController(fakeService, logger);

        var result = await controller.ListTagsForKnowledgebaseAsync(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<List<TagResponseDto>>>(ok.Value);
        Assert.Equal(0, api.Code);
        Assert.Equal("Ok", api.Message);
        Assert.NotNull(api.Data);
        Assert.Single(api.Data!);
        Assert.Same(tag, api.Data![0]);
        Assert.DoesNotContain(logger.Logs, log =>
            log.Message.Contains("knowledgebase tags", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetPublishedByTagAsync_ReturnsBadRequest_WhenPageIsInvalid()
    {
        var fakeService = new FakeKnowledgebaseService();
        var controller = CreateController(fakeService);

        var result = await controller.GetPublishedByTagAsync(1, 0, 20, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<PagedResponse<KnowledgebaseArticleListDto>>>(badRequest.Value);
        Assert.Equal(400, api.Code);
        Assert.Contains("Page must be greater than 0", api.Message);
    }

    [Fact]
    public async Task GetPublishedByTagAsync_ReturnsBadRequest_WhenPageSizeIsTooSmall()
    {
        var fakeService = new FakeKnowledgebaseService();
        var controller = CreateController(fakeService);

        var result = await controller.GetPublishedByTagAsync(1, 1, 0, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<PagedResponse<KnowledgebaseArticleListDto>>>(badRequest.Value);
        Assert.Equal(400, api.Code);
        Assert.Contains("PageSize must be between 1 and 100", api.Message);
    }

    [Fact]
    public async Task GetPublishedByTagAsync_ReturnsBadRequest_WhenPageSizeIsTooLarge()
    {
        var fakeService = new FakeKnowledgebaseService();
        var controller = CreateController(fakeService);

        var result = await controller.GetPublishedByTagAsync(1, 1, 101, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<PagedResponse<KnowledgebaseArticleListDto>>>(badRequest.Value);
        Assert.Equal(400, api.Code);
        Assert.Contains("PageSize must be between 1 and 100", api.Message);
    }

    [Fact]
    public async Task GetPublishedByTagAsync_ReturnsNotFound_WhenTagDoesNotExist()
    {
        var fakeService = new FakeKnowledgebaseService { TagExists = false };
        var controller = CreateController(fakeService);

        var result = await controller.GetPublishedByTagAsync(42, 1, 20, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<PagedResponse<KnowledgebaseArticleListDto>>>(notFound.Value);
        Assert.Equal(404, api.Code);
        Assert.Equal("Tag not found.", api.Message);
        Assert.Null(api.Data);
    }

    [Fact]
    public async Task GetPublishedByTagAsync_ReturnsOk_WithMessage_WhenNoPublishedArticles()
    {
        var fakeService = new FakeKnowledgebaseService
        {
            TagExists = true,
            Articles = new List<KnowledgebaseArticleListDto>()
        };
        var controller = CreateController(fakeService);

        var result = await controller.GetPublishedByTagAsync(7, 1, 20, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<PagedResponse<KnowledgebaseArticleListDto>>>(ok.Value);
        Assert.Equal(0, api.Code);
        Assert.Equal("No published articles for this tag.", api.Message);
        Assert.NotNull(api.Data);
        Assert.Empty(api.Data!.Items);
    }

    [Fact]
    public async Task GetPublishedByTagAsync_ReturnsArticles_WhenTagExistsAndHasPublished()
    {
        var article = new KnowledgebaseArticleListDto(
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            1,
            "Title",
            "Subtitle",
            "Icon",
            DateTime.Parse("2025-01-01T00:00:00Z"),
            DateTime.Parse("2025-01-02T00:00:00Z"));

        var fakeService = new FakeKnowledgebaseService
        {
            TagExists = true,
            Articles = new List<KnowledgebaseArticleListDto> { article }
        };
        var controller = CreateController(fakeService);

        var result = await controller.GetPublishedByTagAsync(5, 1, 20, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<PagedResponse<KnowledgebaseArticleListDto>>>(ok.Value);
        Assert.Equal(0, api.Code);
        Assert.Equal("Ok", api.Message);
        Assert.NotNull(api.Data);
        Assert.Single(api.Data!.Items);
        Assert.Same(article, api.Data.Items.First());
    }

    [Fact]
    public async Task GetPublishedByIdAsync_ReturnsNotFound_WhenArticleDoesNotExist()
    {
        var fakeService = new FakeKnowledgebaseService { Article = null };
        var controller = CreateController(fakeService);

        var result = await controller.GetPublishedByIdAsync(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<KnowledgebaseArticleDetailDto>>(notFound.Value);
        Assert.Equal(404, api.Code);
        Assert.Equal("Article not found or not published.", api.Message);
        Assert.Null(api.Data);
    }

    [Fact]
    public async Task GetPublishedByIdAsync_ReturnsArticle_WhenFoundAndPublished()
    {
        var article = new KnowledgebaseArticleDetailDto(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            9,
            "Detail Title",
            "Detail Subtitle",
            "IconName",
            "Content",
            true,
            DateTime.Parse("2025-02-01T00:00:00Z"),
            DateTime.Parse("2025-02-02T00:00:00Z"));

        var fakeService = new FakeKnowledgebaseService
        {
            Article = article
        };
        var logger = new TestLogger<KnowledgebaseController>();
        var controller = CreateController(fakeService, logger);

        var result = await controller.GetPublishedByIdAsync(article.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<KnowledgebaseArticleDetailDto>>(ok.Value);
        Assert.Equal(0, api.Code);
        Assert.Equal("Ok", api.Message);
        Assert.NotNull(api.Data);
        Assert.Same(article, api.Data);
        Assert.Contains(logger.Logs, log =>
            log.Level == LogLevel.Information &&
            log.Message.Contains("Found knowledgebase article", StringComparison.OrdinalIgnoreCase) &&
            log.Message.Contains(article.Id.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SearchPublishedArticlesAsync_ReturnsBadRequest_WhenKeywordMissing()
    {
        var fakeService = new FakeKnowledgebaseService();
        var controller = CreateController(fakeService);

        var result = await controller.SearchPublishedArticlesAsync("   ", CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<List<KnowledgebaseArticleListDto>>>(badRequest.Value);
        Assert.Equal(400, api.Code);
        Assert.Equal("Keyword is required.", api.Message);
        Assert.Null(api.Data);
        Assert.Null(fakeService.LastSearchKeyword);
    }

    [Fact]
    public async Task SearchPublishedArticlesAsync_ReturnsBadRequest_WhenKeywordTooLong()
    {
        var fakeService = new FakeKnowledgebaseService();
        var controller = CreateController(fakeService);

        var longKeyword = new string('a', 257);
        var result = await controller.SearchPublishedArticlesAsync(longKeyword, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<List<KnowledgebaseArticleListDto>>>(badRequest.Value);
        Assert.Equal(400, api.Code);
        Assert.Equal("Keyword is too long. Maximum length is 256 characters.", api.Message);
        Assert.Null(api.Data);
        Assert.Null(fakeService.LastSearchKeyword);
    }

    [Fact]
    public async Task SearchPublishedArticlesAsync_ReturnsOk_WithMessage_WhenNoArticles()
    {
        var fakeService = new FakeKnowledgebaseService
        {
            SearchResults = new List<KnowledgebaseArticleListDto>()
        };
        var controller = CreateController(fakeService);

        var result = await controller.SearchPublishedArticlesAsync("notfound", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<List<KnowledgebaseArticleListDto>>>(ok.Value);
        Assert.Equal(0, api.Code);
        Assert.Equal("No articles found for this keyword.", api.Message);
        Assert.NotNull(api.Data);
        Assert.Empty(api.Data!);
        Assert.Equal("notfound", fakeService.LastSearchKeyword);
    }

    [Fact]
    public async Task SearchPublishedArticlesAsync_ReturnsArticles_WhenFound()
    {
        var article = new KnowledgebaseArticleListDto(
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            1,
            "Match Title",
            "Match Subtitle",
            "match_icon",
            DateTime.Parse("2025-05-01T00:00:00Z"),
            DateTime.Parse("2025-05-02T00:00:00Z"));

        var fakeService = new FakeKnowledgebaseService
        {
            SearchResults = new List<KnowledgebaseArticleListDto> { article }
        };
        var logger = new TestLogger<KnowledgebaseController>();
        var controller = CreateController(fakeService, logger);

        var result = await controller.SearchPublishedArticlesAsync("  match  ", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<List<KnowledgebaseArticleListDto>>>(ok.Value);
        Assert.Equal(0, api.Code);
        Assert.Equal("Ok", api.Message);
        Assert.NotNull(api.Data);
        Assert.Single(api.Data!);
        Assert.Same(article, api.Data![0]);
        Assert.Equal("match", fakeService.LastSearchKeyword);
        Assert.Contains(logger.Logs, log =>
            log.Level == LogLevel.Information &&
            log.Message.Contains("Found", StringComparison.OrdinalIgnoreCase) &&
            log.Message.Contains("match", StringComparison.OrdinalIgnoreCase));
    }

    private static KnowledgebaseController CreateController(
        IKnowledgebaseService service,
        ILogger<KnowledgebaseController>? logger = null)
    {
        return new KnowledgebaseController(service, logger ?? NullLogger<KnowledgebaseController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    private sealed class FakeKnowledgebaseService : IKnowledgebaseService
    {
        public bool TagExists { get; set; } = true;
        public List<KnowledgebaseArticleListDto>? Articles { get; set; }
        public KnowledgebaseArticleDetailDto? Article { get; set; }
        public List<TagResponseDto>? Tags { get; set; }
        public List<KnowledgebaseArticleListDto>? SearchResults { get; set; }
        public int? LastTagIdForExists { get; private set; }
        public int? LastTagIdForList { get; private set; }
        public Guid? LastArticleId { get; private set; }
        public CreateArticleRequestDto? LastCreateRequest { get; private set; }
        public string? LastSearchKeyword { get; private set; }

        public Task<bool> TagExistsAsync(int tagId, CancellationToken cancellationToken = default)
        {
            LastTagIdForExists = tagId;
            return Task.FromResult(TagExists);
        }

        public Task<PagedResponse<KnowledgebaseArticleListDto>> GetPublishedByTagAsync(int tagId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            LastTagIdForList = tagId;
            var items = Articles ?? new List<KnowledgebaseArticleListDto>();
            var pagedResponse = new PagedResponse<KnowledgebaseArticleListDto>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = items.Count,
                TotalPages = (int)Math.Ceiling((double)items.Count / pageSize)
            };
            return Task.FromResult(pagedResponse);
        }

        public Task<KnowledgebaseArticleDetailDto?> GetArticleByIdAsync(Guid articleId,
            CancellationToken cancellationToken = default)
        {
            LastArticleId = articleId;
            return Task.FromResult(Article);
        }

        public Task<List<KnowledgebaseArticleListDto>> SearchPublishedArticlesAsync(
            string keyword,
            CancellationToken cancellationToken = default)
        {
            LastSearchKeyword = keyword;
            return Task.FromResult(SearchResults ?? new List<KnowledgebaseArticleListDto>());
        }

        public Task<List<TagResponseDto>> ListTagsForArticlesAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Tags ?? new List<TagResponseDto>());
        }

        public Task<KnowledgebaseArticleDetailDto> CreateArticleAsync(
            CreateArticleRequestDto request,
            CancellationToken cancellationToken = default)
        {
            LastCreateRequest = request;
            if (Article is null)
            {
                Article = new KnowledgebaseArticleDetailDto(
                    Guid.NewGuid(),
                    request.TagId,
                    request.Title,
                    request.Subtitle,
                    request.IconName,
                    request.Content,
                    request.IsPublished,
                    DateTime.UtcNow,
                    DateTime.UtcNow);
            }
            return Task.FromResult(Article);
        }

        public Task<List<KnowledgebaseArticleListDto>> GetFeaturedArticlesAsync(
            int count,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new List<KnowledgebaseArticleListDto>());
        }
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<LogEntry> Logs { get; } = new();

        IDisposable? ILogger.BeginScope<TState>(TState state) => NullScope.Instance;

        bool ILogger.IsEnabled(LogLevel logLevel) => true;

        void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Logs.Add(new LogEntry(logLevel, eventId, formatter(state, exception), exception));
        }

        public sealed record LogEntry(LogLevel Level, EventId EventId, string Message, Exception? Exception);

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();
            public void Dispose() { }
        }
    }
}
