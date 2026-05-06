using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using backend.Controllers.Admin;
using backend.Dtos;
using backend.Dtos.Knowledgebase;
using backend.Dtos.Tags;
using backend.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Xunit;

namespace backend.Tests.Controllers;

public class KnowledgebaseAdminControllerTests
{
    [Fact]
    public async Task CreateArticleAsync_ReturnsNotFound_WhenTagDoesNotExist()
    {
        var fakeService = new FakeKnowledgebaseService { TagExists = false };
        var controller = CreateController(fakeService);

        var request = new CreateArticleRequestDto
        {
            Title = "New Article",
            Content = "Body",
            TagId = 42
        };

        var result = await controller.CreateArticleAsync(request, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<KnowledgebaseArticleDetailDto>>(notFound.Value);
        Assert.Equal(404, api.Code);
        Assert.Equal("Tag not found.", api.Message);
        Assert.Null(api.Data);
        Assert.Equal(42, fakeService.LastTagIdForExists);
        Assert.Null(fakeService.LastCreateRequest);
    }

    [Fact]
    public async Task CreateArticleAsync_ReturnsCreated_WhenTagExists()
    {
        var article = new KnowledgebaseArticleDetailDto(
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            7,
            "Created Title",
            "Sub",
            "Icon",
            "Content",
            true,
            DateTime.Parse("2025-03-01T00:00:00Z"),
            DateTime.Parse("2025-03-02T00:00:00Z"));

        var fakeService = new FakeKnowledgebaseService
        {
            TagExists = true,
            Article = article
        };
        var controller = CreateController(fakeService);

        var request = new CreateArticleRequestDto
        {
            Title = "Created Title",
            Subtitle = "Sub",
            IconName = "Icon",
            Content = "Content",
            TagId = 7,
            IsPublished = true
        };

        var result = await controller.CreateArticleAsync(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtRouteResult>(result.Result);
        Assert.Equal(201, created.StatusCode);
        Assert.Equal("GetKnowledgebaseArticleById", created.RouteName);
        Assert.Equal(article.Id, created.RouteValues?["articleId"]);

        var api = Assert.IsType<ApiResponse<KnowledgebaseArticleDetailDto>>(created.Value);
        Assert.Equal(201, api.Code);
        Assert.Equal("Created", api.Message);
        Assert.NotNull(api.Data);
        Assert.Same(article, api.Data);
        Assert.Equal(7, fakeService.LastTagIdForExists);
        Assert.NotNull(fakeService.LastCreateRequest);
        Assert.Equal(request.Title, fakeService.LastCreateRequest!.Title);
    }

    private static KnowledgebaseAdminController CreateController(IKnowledgebaseService service) =>
        new(service, NullLogger.Instance);

    private sealed class FakeKnowledgebaseService : IKnowledgebaseService
    {
        public bool TagExists { get; set; } = true;
        public KnowledgebaseArticleDetailDto? Article { get; set; }
        public CreateArticleRequestDto? LastCreateRequest { get; private set; }
        public int? LastTagIdForExists { get; private set; }

        public Task<bool> TagExistsAsync(int tagId, CancellationToken cancellationToken = default)
        {
            LastTagIdForExists = tagId;
            return Task.FromResult(TagExists);
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

        public Task<PagedResponse<KnowledgebaseArticleListDto>> GetPublishedByTagAsync(int tagId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PagedResponse<KnowledgebaseArticleListDto>());

        public Task<KnowledgebaseArticleDetailDto?> GetArticleByIdAsync(Guid articleId, CancellationToken cancellationToken = default) =>
            Task.FromResult<KnowledgebaseArticleDetailDto?>(null);

        public Task<List<TagResponseDto>> ListTagsForArticlesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<TagResponseDto>());

        public Task<List<KnowledgebaseArticleListDto>> SearchPublishedArticlesAsync(
            string keyword,
            CancellationToken cancellationToken = default) => Task.FromResult(new List<KnowledgebaseArticleListDto>());

        public Task<List<KnowledgebaseArticleListDto>> GetFeaturedArticlesAsync(
            int count,
            CancellationToken cancellationToken = default) => Task.FromResult(new List<KnowledgebaseArticleListDto>());
    }

    private sealed class NullLogger : ILogger<KnowledgebaseAdminController>
    {
        public static NullLogger Instance { get; } = new();

        IDisposable? ILogger.BeginScope<TState>(TState state) => null;

        bool ILogger.IsEnabled(LogLevel logLevel) => false;

        void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
        }
    }
}
