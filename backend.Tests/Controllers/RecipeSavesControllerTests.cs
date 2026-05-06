using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using backend.Controllers;
using backend.Dtos;
using backend.Dtos.Recipes;
using backend.Interfaces;
using backend.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace backend.Tests.Controllers;

public class RecipeSavesControllerTests
{
    private static readonly Guid RecipeId = Guid.Parse("00000000-0000-0000-0000-000000000123");

    [Fact]
    public async Task ToggleSaveAsync_ReturnsUnauthorized_WhenClerkIdMissing()
    {
        var controller = CreateController(new FakeRecipeSaveService(), new ClaimsPrincipal(new ClaimsIdentity()));

        var result = await controller.ToggleSaveAsync(RecipeId, CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<RecipeSaveResponseDto>>(unauthorized.Value);
        Assert.Equal(401, api.Code);
        Assert.Equal("Could not determine Clerk user id from token.", api.Message);
        Assert.Null(api.Data);
    }

    [Fact]
    public async Task ToggleSaveAsync_ReturnsNotFound_WhenServiceReturnsNull()
    {
        var fakeService = new FakeRecipeSaveService { Response = null };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));

        var result = await controller.ToggleSaveAsync(RecipeId, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<RecipeSaveResponseDto>>(notFound.Value);
        Assert.Equal(404, api.Code);
        Assert.Equal("User or recipe not found.", api.Message);
        Assert.Null(api.Data);
        Assert.Equal(RecipeId, fakeService.LastRecipeId);
        Assert.Equal("clerk_123", fakeService.LastClerkUserId);
    }

    [Fact]
    public async Task ToggleSaveAsync_ReturnsOk_WhenServiceReturnsResponse()
    {
        var response = new RecipeSaveResponseDto(RecipeId, IsSaved: true, SavesCount: 7);
        var fakeService = new FakeRecipeSaveService { Response = response };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_abc"));

        var result = await controller.ToggleSaveAsync(RecipeId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<RecipeSaveResponseDto>>(ok.Value);
        Assert.Equal(0, api.Code);
        Assert.Equal("Ok", api.Message);
        Assert.Same(response, api.Data);
        Assert.Equal(RecipeId, fakeService.LastRecipeId);
        Assert.Equal("clerk_abc", fakeService.LastClerkUserId);
    }

    [Fact]
    public async Task GetMySavedRecipes_ReturnsUnauthorized_WhenClerkIdMissing()
    {
        var controller = CreateController(new FakeRecipeSaveService(), new ClaimsPrincipal(new ClaimsIdentity()));

        var result = await controller.GetMySavedRecipes(page: 1, pageSize: 20, category: null, CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<IReadOnlyList<MySavedRecipeCardDto>>>(unauthorized.Value);
        Assert.Equal(401, api.Code);
        Assert.Equal("Could not determine Clerk user id from token.", api.Message);
        Assert.Null(api.Data);
    }

    [Fact]
    public async Task GetMySavedRecipes_ReturnsUnauthorized_WhenServiceReturnsNull()
    {
        var fakeService = new FakeRecipeSaveService { MySaves = null };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));

        var result = await controller.GetMySavedRecipes(page: 2, pageSize: 10, category: null, CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<IReadOnlyList<MySavedRecipeCardDto>>>(unauthorized.Value);
        Assert.Equal(401, api.Code);
        Assert.Equal("User not found.", api.Message);
        Assert.Null(api.Data);
        Assert.Equal("clerk_123", fakeService.LastClerkUserId);
        Assert.Equal(2, fakeService.LastPage);
        Assert.Equal(10, fakeService.LastPageSize);
    }

    [Fact]
    public async Task GetMySavedRecipes_ReturnsOk_WhenServiceReturnsList()
    {
        var savedAt = DateTime.UtcNow;
        var cards = new List<MySavedRecipeCardDto>
        {
            new(
                Id: RecipeId,
                Title: "Recipe",
                Description: "desc",
                CoverImageUrl: "https://example.com/cover.png",
                AuthorId: Guid.Parse("00000000-0000-0000-0000-000000000111"),
                AuthorName: "Alice",
                SavedCount: 4,
                SavedByMe: true,
                SavedAt: savedAt,
                Type: RecipeType.User)
        };
        var fakeService = new FakeRecipeSaveService { MySaves = cards };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_abc"));

        var result = await controller.GetMySavedRecipes(page: 1, pageSize: 20, category: null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<IReadOnlyList<MySavedRecipeCardDto>>>(ok.Value);
        Assert.Equal(0, api.Code);
        Assert.Equal("Ok", api.Message);
        Assert.Same(cards, api.Data);
        Assert.Equal("clerk_abc", fakeService.LastClerkUserId);
        Assert.Equal(1, fakeService.LastPage);
        Assert.Equal(20, fakeService.LastPageSize);
    }

    [Fact]
    public async Task GetMySavesCount_ReturnsUnauthorized_WhenClerkIdMissing()
    {
        var controller = CreateController(new FakeRecipeSaveService(), new ClaimsPrincipal(new ClaimsIdentity()));

        var result = await controller.GetMySavesCount(category: null, CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<MeSavesCountDto>>(unauthorized.Value);
        Assert.Equal(401, api.Code);
        Assert.Equal("Could not determine Clerk user id from token.", api.Message);
        Assert.Null(api.Data);
    }

    [Fact]
    public async Task GetMySavesCount_ReturnsUnauthorized_WhenServiceReturnsNull()
    {
        var fakeService = new FakeRecipeSaveService { SavesCount = null };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));

        var result = await controller.GetMySavesCount(category: null, CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<MeSavesCountDto>>(unauthorized.Value);
        Assert.Equal(401, api.Code);
        Assert.Equal("User not found.", api.Message);
        Assert.Null(api.Data);
        Assert.Equal("clerk_123", fakeService.LastClerkUserId);
    }

    [Fact]
    public async Task GetMySavesCount_ReturnsOk_WhenServiceReturnsCount()
    {
        var fakeService = new FakeRecipeSaveService { SavesCount = 9 };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_abc"));

        var result = await controller.GetMySavesCount(category: null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<MeSavesCountDto>>(ok.Value);
        Assert.Equal(0, api.Code);
        Assert.Equal("Ok", api.Message);
        Assert.Equal(9, api.Data?.Count);
        Assert.Equal("clerk_abc", fakeService.LastClerkUserId);
    }

    private static RecipeSavesController CreateController(IRecipeSaveService service, ClaimsPrincipal user)
    {
        return new RecipeSavesController(service, NullLogger<RecipeSavesController>.Instance)
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

    private sealed class FakeRecipeSaveService : IRecipeSaveService
    {
        public RecipeSaveResponseDto? Response { get; set; }
        public IReadOnlyList<MySavedRecipeCardDto>? MySaves { get; set; } = Array.Empty<MySavedRecipeCardDto>();
        public int? SavesCount { get; set; } = 0;
        public Guid? LastRecipeId { get; private set; }
        public string? LastClerkUserId { get; private set; }
        public int? LastPage { get; private set; }
        public int? LastPageSize { get; private set; }
        public SavesCategory? LastCategory { get; private set; }

        public Task<RecipeSaveResponseDto?> ToggleSaveAsync(Guid recipeId, string clerkUserId,
            CancellationToken cancellationToken = default)
        {
            LastRecipeId = recipeId;
            LastClerkUserId = clerkUserId;
            return Task.FromResult(Response);
        }

        public Task<IReadOnlyList<MySavedRecipeCardDto>?> GetMySavedRecipesAsync(string clerkUserId, int page,
            int pageSize, CancellationToken cancellationToken = default)
        {
            LastClerkUserId = clerkUserId;
            LastPage = page;
            LastPageSize = pageSize;
            return Task.FromResult(MySaves);
        }

        public Task<IReadOnlyList<MySavedRecipeCardDto>?> GetMySavedRecipesAsync(string clerkUserId, int page,
            int pageSize, SavesCategory category, CancellationToken cancellationToken = default)
        {
            LastClerkUserId = clerkUserId;
            LastPage = page;
            LastPageSize = pageSize;
            LastCategory = category;
            return Task.FromResult(MySaves);
        }

        public Task<int?> GetMySavesCountAsync(string clerkUserId, CancellationToken cancellationToken = default)
        {
            LastClerkUserId = clerkUserId;
            return Task.FromResult(SavesCount);
        }

        public Task<int?> GetMySavesCountAsync(string clerkUserId, SavesCategory category,
            CancellationToken cancellationToken = default)
        {
            LastClerkUserId = clerkUserId;
            LastCategory = category;
            return Task.FromResult(SavesCount);
        }
    }
}

