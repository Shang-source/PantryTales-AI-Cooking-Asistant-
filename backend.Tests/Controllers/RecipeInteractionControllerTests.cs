using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using backend.Controllers;
using backend.Dtos;
using backend.Dtos.Interactions;
using backend.Interfaces;
using backend.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace backend.Tests.Controllers;

public class RecipeInteractionControllerTests
{
    private static readonly Guid RecipeId = Guid.Parse("00000000-0000-0000-0000-000000000456");

    #region LogInteractionAsync Tests

    [Fact]
    public async Task LogInteractionAsync_ReturnsUnauthorized_WhenClerkIdMissing()
    {
        var controller = CreateController(new FakeRecipeInteractionService(), new ClaimsPrincipal(new ClaimsIdentity()));

        var request = new LogInteractionRequestDto(RecipeId, RecipeInteractionEventType.Click);
        var result = await controller.LogInteractionAsync(request, CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse>(unauthorized.Value);
        Assert.Equal(401, api.Code);
        Assert.Equal("Could not determine user from token.", api.Message);
    }

    [Fact]
    public async Task LogInteractionAsync_ReturnsNotFound_WhenServiceReturnsFalse()
    {
        var fakeService = new FakeRecipeInteractionService { LogInteractionResult = false };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));

        var request = new LogInteractionRequestDto(RecipeId, RecipeInteractionEventType.Click, "home_feed");
        var result = await controller.LogInteractionAsync(request, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse>(notFound.Value);
        Assert.Equal(404, api.Code);
        Assert.Equal("User or recipe not found.", api.Message);
        Assert.Equal("clerk_123", fakeService.LastClerkUserId);
        Assert.Equal(RecipeId, fakeService.LastRecipeId);
        Assert.Equal(RecipeInteractionEventType.Click, fakeService.LastEventType);
    }

    [Fact]
    public async Task LogInteractionAsync_ReturnsOk_WhenServiceReturnsTrue()
    {
        var fakeService = new FakeRecipeInteractionService { LogInteractionResult = true };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_abc"));

        var request = new LogInteractionRequestDto(RecipeId, RecipeInteractionEventType.Dwell, "recipe_detail", "session_123", 45);
        var result = await controller.LogInteractionAsync(request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<object>>(ok.Value);
        Assert.Equal(0, api.Code);
        Assert.Equal("Interaction logged.", api.Message);
        Assert.Equal("recipe_detail", fakeService.LastSource);
        Assert.Equal("session_123", fakeService.LastSessionId);
        Assert.Equal(45, fakeService.LastDwellSeconds);
    }

    #endregion

    #region LogImpressionsAsync Tests

    [Fact]
    public async Task LogImpressionsAsync_ReturnsUnauthorized_WhenClerkIdMissing()
    {
        var controller = CreateController(new FakeRecipeInteractionService(), new ClaimsPrincipal(new ClaimsIdentity()));

        var request = new LogImpressionsRequestDto([RecipeId]);
        var result = await controller.LogImpressionsAsync(request, CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse>(unauthorized.Value);
        Assert.Equal(401, api.Code);
    }

    [Fact]
    public async Task LogImpressionsAsync_ReturnsBadRequest_WhenRecipeIdsEmpty()
    {
        var controller = CreateController(new FakeRecipeInteractionService(), BuildPrincipal("clerk_123"));

        var request = new LogImpressionsRequestDto([]);
        var result = await controller.LogImpressionsAsync(request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse>(badRequest.Value);
        Assert.Equal(400, api.Code);
        Assert.Equal("RecipeIds cannot be empty.", api.Message);
    }

    [Fact]
    public async Task LogImpressionsAsync_ReturnsBadRequest_WhenExceeds100Impressions()
    {
        var controller = CreateController(new FakeRecipeInteractionService(), BuildPrincipal("clerk_123"));

        var recipeIds = new List<Guid>();
        for (int i = 0; i < 101; i++)
            recipeIds.Add(Guid.NewGuid());

        var request = new LogImpressionsRequestDto(recipeIds);
        var result = await controller.LogImpressionsAsync(request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse>(badRequest.Value);
        Assert.Equal(400, api.Code);
        Assert.Equal("Cannot log more than 100 impressions at once.", api.Message);
    }

    [Fact]
    public async Task LogImpressionsAsync_ReturnsOk_WithCount()
    {
        var fakeService = new FakeRecipeInteractionService { LogImpressionsResult = 3 };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_abc"));

        var recipeIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var request = new LogImpressionsRequestDto(recipeIds, "search_results", "session_789");
        var result = await controller.LogImpressionsAsync(request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<object>>(ok.Value);
        Assert.Equal(0, api.Code);
        Assert.Contains("3", api.Message);
        Assert.Equal("search_results", fakeService.LastSource);
        Assert.Equal("session_789", fakeService.LastSessionId);
    }

    #endregion

    #region GetStatsAsync Tests

    [Fact]
    public async Task GetStatsAsync_ReturnsBadRequest_WhenDaysOutOfRange()
    {
        var controller = CreateController(new FakeRecipeInteractionService(), BuildPrincipal("clerk_123"));

        var result = await controller.GetStatsAsync(RecipeId, days: 0);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse>(badRequest.Value);
        Assert.Equal(400, api.Code);
        Assert.Equal("Days must be between 1 and 365.", api.Message);
    }

    [Fact]
    public async Task GetStatsAsync_ReturnsBadRequest_WhenDaysExceeds365()
    {
        var controller = CreateController(new FakeRecipeInteractionService(), BuildPrincipal("clerk_123"));

        var result = await controller.GetStatsAsync(RecipeId, days: 400);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse>(badRequest.Value);
        Assert.Equal(400, api.Code);
    }

    [Fact]
    public async Task GetStatsAsync_ReturnsNotFound_WhenServiceReturnsNull()
    {
        var fakeService = new FakeRecipeInteractionService { GetStatsResult = null };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));

        var result = await controller.GetStatsAsync(RecipeId, days: 30);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse>(notFound.Value);
        Assert.Equal(404, api.Code);
        Assert.Equal("Recipe not found.", api.Message);
    }

    [Fact]
    public async Task GetStatsAsync_ReturnsOk_WithStats()
    {
        var stats = new RecipeInteractionStatsDto(RecipeId, 100, 10, 5, 3, 8, 2, 1, 0.1, 30);
        var fakeService = new FakeRecipeInteractionService { GetStatsResult = stats };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_abc"));

        var result = await controller.GetStatsAsync(RecipeId, days: 30);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<RecipeInteractionStatsDto>>(ok.Value);
        Assert.Equal(0, api.Code);
        Assert.Same(stats, api.Data);
    }

    #endregion

    #region Helpers

    private static RecipeInteractionController CreateController(IRecipeInteractionService service, ClaimsPrincipal user)
    {
        return new RecipeInteractionController(service, NullLogger<RecipeInteractionController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            }
        };
    }

    private static ClaimsPrincipal BuildPrincipal(string clerkUserId)
    {
        var identity = new ClaimsIdentity([new Claim("clerk_user_id", clerkUserId)], "mock");
        return new ClaimsPrincipal(identity);
    }

    private sealed class FakeRecipeInteractionService : IRecipeInteractionService
    {
        public bool LogInteractionResult { get; set; } = true;
        public int LogImpressionsResult { get; set; }
        public RecipeInteractionStatsDto? GetStatsResult { get; set; }

        public string? LastClerkUserId { get; private set; }
        public Guid? LastRecipeId { get; private set; }
        public RecipeInteractionEventType? LastEventType { get; private set; }
        public string? LastSource { get; private set; }
        public string? LastSessionId { get; private set; }
        public int? LastDwellSeconds { get; private set; }

        public Task<bool> LogInteractionAsync(
            string clerkUserId,
            Guid recipeId,
            RecipeInteractionEventType eventType,
            string? source = null,
            string? sessionId = null,
            int? dwellSeconds = null,
            CancellationToken cancellationToken = default)
        {
            LastClerkUserId = clerkUserId;
            LastRecipeId = recipeId;
            LastEventType = eventType;
            LastSource = source;
            LastSessionId = sessionId;
            LastDwellSeconds = dwellSeconds;
            return Task.FromResult(LogInteractionResult);
        }

        public Task<int> LogImpressionsAsync(
            string clerkUserId,
            IEnumerable<Guid> recipeIds,
            string? source = null,
            string? sessionId = null,
            CancellationToken cancellationToken = default)
        {
            LastClerkUserId = clerkUserId;
            LastSource = source;
            LastSessionId = sessionId;
            return Task.FromResult(LogImpressionsResult);
        }

        public Task<RecipeInteractionStatsDto?> GetRecipeStatsAsync(
            Guid recipeId,
            int days = 30,
            CancellationToken cancellationToken = default)
        {
            LastRecipeId = recipeId;
            return Task.FromResult(GetStatsResult);
        }
    }

    #endregion
}
