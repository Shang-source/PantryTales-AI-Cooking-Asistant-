using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using backend.Controllers;
using backend.Dtos;
using backend.Dtos.Users;
using backend.Interfaces;
using backend.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace backend.Tests.Controllers;

public class RecommendedRecipesControllerTests
{
    private static readonly Guid DefaultUserId = Guid.Parse("00000000-0000-0000-0000-000000000011");
    private static readonly Guid DefaultRecipeId = Guid.Parse("00000000-0000-0000-0000-000000000022");

    #region GetRecommendations Tests

    [Fact]
    public async Task GetRecommendations_ReturnsUnauthorized_WhenClerkUserIdMissing()
    {
        var fakeRecommendedService = new FakeRecommendedRecipeService();
        var fakeUserService = new FakeUserService();
        var controller = CreateController(fakeRecommendedService, fakeUserService, new ClaimsPrincipal(new ClaimsIdentity()));

        var result = await controller.GetRecommendations(cancellationToken: CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<RecommendedRecipesResponse>>(unauthorized.Value);
        Assert.Equal(401, response.Code);
        Assert.Equal("Could not determine user from token.", response.Message);
    }

    [Fact]
    public async Task GetRecommendations_ReturnsUnauthorized_WhenUserNotFound()
    {
        var fakeRecommendedService = new FakeRecommendedRecipeService();
        var fakeUserService = new FakeUserService { UserResult = null };
        var controller = CreateController(fakeRecommendedService, fakeUserService, BuildPrincipal("clerk_123"));

        var result = await controller.GetRecommendations(cancellationToken: CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<RecommendedRecipesResponse>>(unauthorized.Value);
        Assert.Equal(401, response.Code);
        Assert.Equal("User not found.", response.Message);
    }

    [Fact]
    public async Task GetRecommendations_ReturnsNotFound_WhenUserNotFoundByService()
    {
        var fakeRecommendedService = new FakeRecommendedRecipeService
        {
            RecommendationsResult = new RecommendedRecipeResult(RecommendedRecipeResultStatus.UserNotFound, ErrorMessage: "User not found")
        };
        var fakeUserService = new FakeUserService { UserResult = NewUserDto() };
        var controller = CreateController(fakeRecommendedService, fakeUserService, BuildPrincipal("clerk_123"));

        var result = await controller.GetRecommendations(cancellationToken: CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<RecommendedRecipesResponse>>(notFound.Value);
        Assert.Equal(404, response.Code);
    }

    [Fact]
    public async Task GetRecommendations_ReturnsOk_WithEmptyList_WhenNoRecipesAvailable()
    {
        var fakeRecommendedService = new FakeRecommendedRecipeService
        {
            RecommendationsResult = new RecommendedRecipeResult(RecommendedRecipeResultStatus.NoRecipesAvailable)
        };
        var fakeUserService = new FakeUserService { UserResult = NewUserDto() };
        var controller = CreateController(fakeRecommendedService, fakeUserService, BuildPrincipal("clerk_123"));

        var result = await controller.GetRecommendations(cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<RecommendedRecipesResponse>>(ok.Value);
        Assert.Equal(0, response.Code);
        Assert.NotNull(response.Data);
        Assert.Empty(response.Data.Recipes);
        Assert.Equal("No recipes available yet", response.Data.Message);
    }

    [Fact]
    public async Task GetRecommendations_ReturnsOk_WhenSucceeds()
    {
        var recipes = new List<RecommendedRecipeDto>
        {
            NewRecommendedRecipeDto()
        };
        var fakeRecommendedService = new FakeRecommendedRecipeService
        {
            RecommendationsResult = new RecommendedRecipeResult(RecommendedRecipeResultStatus.Success, recipes, 1)
        };
        var fakeUserService = new FakeUserService { UserResult = NewUserDto() };
        var controller = CreateController(fakeRecommendedService, fakeUserService, BuildPrincipal("clerk_123"));

        var result = await controller.GetRecommendations(limit: 20, offset: 0, cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<RecommendedRecipesResponse>>(ok.Value);
        Assert.Equal(0, response.Code);
        Assert.NotNull(response.Data);
        Assert.Single(response.Data.Recipes);
        Assert.Equal(1, response.Data.TotalCount);
        Assert.Equal("clerk_123", fakeUserService.LastClerkUserId);
    }

    [Fact]
    public async Task GetRecommendations_PassesSearchAndSeedParameters()
    {
        var recipes = new List<RecommendedRecipeDto>();
        var fakeRecommendedService = new FakeRecommendedRecipeService
        {
            RecommendationsResult = new RecommendedRecipeResult(RecommendedRecipeResultStatus.Success, recipes, 0)
        };
        var fakeUserService = new FakeUserService { UserResult = NewUserDto() };
        var controller = CreateController(fakeRecommendedService, fakeUserService, BuildPrincipal("clerk_123"));

        await controller.GetRecommendations(limit: 10, offset: 5, search: "pasta", seed: "abc123", cancellationToken: CancellationToken.None);

        Assert.Equal(10, fakeRecommendedService.LastLimit);
        Assert.Equal(5, fakeRecommendedService.LastOffset);
        Assert.Equal("pasta", fakeRecommendedService.LastSearch);
        Assert.Equal("abc123", fakeRecommendedService.LastSeed);
    }

    #endregion

    private static RecommendedRecipesController CreateController(
        IRecommendedRecipeService recommendedService,
        IUserService userService,
        ClaimsPrincipal user) =>
        new(recommendedService, userService, NullLogger<RecommendedRecipesController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            }
        };

    private static ClaimsPrincipal BuildPrincipal(string clerkUserId)
    {
        var identity = new ClaimsIdentity([new Claim("clerk_user_id", clerkUserId)], "mock");
        return new ClaimsPrincipal(identity);
    }

    private static UserResponseDto NewUserDto() =>
        new(DefaultUserId, "clerk_123", "test@example.com", "Test User");

    private static RecommendedRecipeDto NewRecommendedRecipeDto() =>
        new(
            DefaultRecipeId,
            "Test Recipe",
            "A test recipe",
            null,
            30,
            RecipeDifficulty.Easy,
            4,
            2,
            10,
            5,
            false,
            ["tag1", "tag2"],
            RecipeType.User,
            DefaultUserId,
            "Author",
            null
        );

    private sealed class FakeRecommendedRecipeService : IRecommendedRecipeService
    {
        public RecommendedRecipeResult RecommendationsResult { get; set; } =
            new(RecommendedRecipeResultStatus.Success, [], 0);

        public int LastLimit { get; private set; }
        public int LastOffset { get; private set; }
        public string? LastSearch { get; private set; }
        public string? LastSeed { get; private set; }

        public Task<RecommendedRecipeResult> GetRecommendationsAsync(
            Guid userId,
            int limit = 20,
            int offset = 0,
            string? search = null,
            string? seed = null,
            CancellationToken cancellationToken = default)
        {
            LastLimit = limit;
            LastOffset = offset;
            LastSearch = search;
            LastSeed = seed;
            return Task.FromResult(RecommendationsResult);
        }
    }

    private sealed class FakeUserService : IUserService
    {
        public UserResponseDto? UserResult { get; set; }
        public string? LastClerkUserId { get; private set; }

        public Task<UserResponseDto> GetOrCreateAsync(UserSyncPayload payload, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<UserResponseDto?> GetByClerkUserIdAsync(string clerkUserId, CancellationToken cancellationToken = default)
        {
            LastClerkUserId = clerkUserId;
            return Task.FromResult(UserResult);
        }

        public Task<UserProfileResponseDto?> GetProfileAsync(string clerkUserId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<UserProfileResponseDto?> UpdateProfileAsync(Guid userId, UpdateUserProfileRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}
