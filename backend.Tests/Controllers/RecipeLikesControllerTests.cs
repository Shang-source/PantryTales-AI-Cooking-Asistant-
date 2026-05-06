using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using backend.Controllers;
using backend.Dtos;
using backend.Dtos.Recipes;
using backend.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace backend.Tests.Controllers;

public class RecipeLikesControllerTests
{
    private static readonly Guid RecipeId = Guid.Parse("00000000-0000-0000-0000-000000000123");

    [Fact]
    public async Task ToggleLikeAsync_ReturnsUnauthorized_WhenClerkIdMissing()
    {
        var controller = CreateController(new FakeRecipeLikeService(), new ClaimsPrincipal(new ClaimsIdentity()));

        var result = await controller.ToggleLikeAsync(RecipeId, CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<RecipeLikeResponseDto>>(unauthorized.Value);
        Assert.Equal(401, api.Code);
        Assert.Equal("Could not determine Clerk user id from token.", api.Message);
        Assert.Null(api.Data);
    }

    [Fact]
    public async Task ToggleLikeAsync_ReturnsNotFound_WhenServiceReturnsNull()
    {
        var fakeService = new FakeRecipeLikeService { Response = null };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));

        var result = await controller.ToggleLikeAsync(RecipeId, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<RecipeLikeResponseDto>>(notFound.Value);
        Assert.Equal(404, api.Code);
        Assert.Equal("User or recipe not found.", api.Message);
        Assert.Null(api.Data);
        Assert.Equal(RecipeId, fakeService.LastRecipeId);
        Assert.Equal("clerk_123", fakeService.LastClerkUserId);
    }

    [Fact]
    public async Task ToggleLikeAsync_ReturnsOk_WhenServiceReturnsResponse()
    {
        var response = new RecipeLikeResponseDto(RecipeId, IsLiked: true, LikesCount: 5);
        var fakeService = new FakeRecipeLikeService { Response = response };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_abc"));

        var result = await controller.ToggleLikeAsync(RecipeId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<RecipeLikeResponseDto>>(ok.Value);
        Assert.Equal(0, api.Code);
        Assert.Equal("Ok", api.Message);
        Assert.Same(response, api.Data);
        Assert.Equal(RecipeId, fakeService.LastRecipeId);
        Assert.Equal("clerk_abc", fakeService.LastClerkUserId);
    }

    [Fact]
    public async Task GetMyLikedRecipes_ReturnsUnauthorized_WhenClerkIdMissing()
    {
        var controller = CreateController(new FakeRecipeLikeService(), new ClaimsPrincipal(new ClaimsIdentity()));

        var result = await controller.GetMyLikedRecipes(page: 1, pageSize: 20, CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<IReadOnlyList<MyLikedRecipeCardDto>>>(unauthorized.Value);
        Assert.Equal(401, api.Code);
        Assert.Equal("Could not determine Clerk user id from token.", api.Message);
        Assert.Null(api.Data);
    }

    [Fact]
    public async Task GetMyLikedRecipes_ReturnsUnauthorized_WhenServiceReturnsNull()
    {
        var fakeService = new FakeRecipeLikeService { MyLikes = null };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));

        var result = await controller.GetMyLikedRecipes(page: 2, pageSize: 10, CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<IReadOnlyList<MyLikedRecipeCardDto>>>(unauthorized.Value);
        Assert.Equal(401, api.Code);
        Assert.Equal("User not found.", api.Message);
        Assert.Null(api.Data);
        Assert.Equal("clerk_123", fakeService.LastClerkUserId);
        Assert.Equal(2, fakeService.LastPage);
        Assert.Equal(10, fakeService.LastPageSize);
    }

    [Fact]
    public async Task GetMyLikedRecipes_ReturnsOk_WhenServiceReturnsList()
    {
        var likedAt = DateTime.UtcNow;
        var cards = new List<MyLikedRecipeCardDto>
        {
            new(
                Id: RecipeId,
                Title: "Recipe",
                Description: "desc",
                CoverImageUrl: "https://example.com/cover.png",
                AuthorId: Guid.Parse("00000000-0000-0000-0000-000000000111"),
                AuthorName: "Alice",
                LikesCount: 3,
                LikedByMe: true,
                LikedAt: likedAt)
        };
        var fakeService = new FakeRecipeLikeService { MyLikes = cards };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_abc"));

        var result = await controller.GetMyLikedRecipes(page: 1, pageSize: 20, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<IReadOnlyList<MyLikedRecipeCardDto>>>(ok.Value);
        Assert.Equal(0, api.Code);
        Assert.Equal("Ok", api.Message);
        Assert.Same(cards, api.Data);
        Assert.Equal("clerk_abc", fakeService.LastClerkUserId);
        Assert.Equal(1, fakeService.LastPage);
        Assert.Equal(20, fakeService.LastPageSize);
    }

    [Fact]
    public async Task GetMyLikesCount_ReturnsUnauthorized_WhenClerkIdMissing()
    {
        var controller = CreateController(new FakeRecipeLikeService(), new ClaimsPrincipal(new ClaimsIdentity()));

        var result = await controller.GetMyLikesCount(CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<MeLikesCountDto>>(unauthorized.Value);
        Assert.Equal(401, api.Code);
        Assert.Equal("Could not determine Clerk user id from token.", api.Message);
        Assert.Null(api.Data);
    }

    [Fact]
    public async Task GetMyLikesCount_ReturnsUnauthorized_WhenServiceReturnsNull()
    {
        var fakeService = new FakeRecipeLikeService { LikesCount = null };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));

        var result = await controller.GetMyLikesCount(CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<MeLikesCountDto>>(unauthorized.Value);
        Assert.Equal(401, api.Code);
        Assert.Equal("User not found.", api.Message);
        Assert.Null(api.Data);
        Assert.Equal("clerk_123", fakeService.LastClerkUserId);
    }

    [Fact]
    public async Task GetMyLikesCount_ReturnsOk_WhenServiceReturnsCount()
    {
        var fakeService = new FakeRecipeLikeService { LikesCount = 12 };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_abc"));

        var result = await controller.GetMyLikesCount(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<MeLikesCountDto>>(ok.Value);
        Assert.Equal(0, api.Code);
        Assert.Equal("Ok", api.Message);
        Assert.Equal(12, api.Data?.Count);
        Assert.Equal("clerk_abc", fakeService.LastClerkUserId);
    }

    private static RecipeLikesController CreateController(IRecipeLikeService service, ClaimsPrincipal user)
    {
        return new RecipeLikesController(service, NullLogger<RecipeLikesController>.Instance)
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

    private sealed class FakeRecipeLikeService : IRecipeLikeService
    {
        public RecipeLikeResponseDto? Response { get; set; }
        public IReadOnlyList<MyLikedRecipeCardDto>? MyLikes { get; set; } = Array.Empty<MyLikedRecipeCardDto>();
        public int? LikesCount { get; set; } = 0;
        public Guid? LastRecipeId { get; private set; }
        public string? LastClerkUserId { get; private set; }
        public int? LastPage { get; private set; }
        public int? LastPageSize { get; private set; }

        public Task<RecipeLikeResponseDto?> ToggleLikeAsync(Guid recipeId, string clerkUserId,
            CancellationToken cancellationToken = default)
        {
            LastRecipeId = recipeId;
            LastClerkUserId = clerkUserId;
            return Task.FromResult(Response);
        }

        public Task<IReadOnlyList<MyLikedRecipeCardDto>?> GetMyLikedRecipesAsync(string clerkUserId, int page,
            int pageSize, CancellationToken cancellationToken = default)
        {
            LastClerkUserId = clerkUserId;
            LastPage = page;
            LastPageSize = pageSize;
            return Task.FromResult(MyLikes);
        }

        public Task<int?> GetMyLikesCountAsync(string clerkUserId, CancellationToken cancellationToken = default)
        {
            LastClerkUserId = clerkUserId;
            return Task.FromResult(LikesCount);
        }
    }
}
