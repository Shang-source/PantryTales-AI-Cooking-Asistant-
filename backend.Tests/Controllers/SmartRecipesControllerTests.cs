using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using backend.Controllers;
using backend.Dtos;
using backend.Dtos.SmartRecipes;
using backend.Dtos.Users;
using backend.Interfaces;
using backend.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace backend.Tests.Controllers;

public class SmartRecipesControllerTests
{
    private static readonly Guid DefaultUserId = Guid.Parse("00000000-0000-0000-0000-000000000011");
    private static readonly Guid DefaultRecipeId = Guid.Parse("00000000-0000-0000-0000-000000000022");
    private static readonly DateTime DefaultCreatedAt = new(2024, 01, 01, 0, 0, 0, DateTimeKind.Utc);

    #region GetSmartRecipes Tests

    [Fact]
    public async Task GetSmartRecipes_ReturnsUnauthorized_WhenClerkUserIdMissing()
    {
        var fakeSmartService = new FakeSmartRecipeService();
        var fakeUserService = new FakeUserService();
        var controller = CreateController(fakeSmartService, fakeUserService, new ClaimsPrincipal(new ClaimsIdentity()));

        var result = await controller.GetSmartRecipes(CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<IReadOnlyList<SmartRecipeDto>>>(unauthorized.Value);
        Assert.Equal(401, response.Code);
    }

    [Fact]
    public async Task GetSmartRecipes_ReturnsUnauthorized_WhenUserNotFound()
    {
        var fakeSmartService = new FakeSmartRecipeService();
        var fakeUserService = new FakeUserService { UserResult = null };
        var controller = CreateController(fakeSmartService, fakeUserService, BuildPrincipal("clerk_123"));

        var result = await controller.GetSmartRecipes(CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<IReadOnlyList<SmartRecipeDto>>>(unauthorized.Value);
        Assert.Equal(401, response.Code);
        Assert.Equal("User not found.", response.Message);
    }

    [Fact]
    public async Task GetSmartRecipes_ReturnsOk_WithMessage_WhenNoInventory()
    {
        var fakeSmartService = new FakeSmartRecipeService
        {
            GetOrGenerateResult = new SmartRecipeResult(SmartRecipeResultStatus.NoInventory)
        };
        var fakeUserService = new FakeUserService { UserResult = NewUserDto() };
        var controller = CreateController(fakeSmartService, fakeUserService, BuildPrincipal("clerk_123"));

        var result = await controller.GetSmartRecipes(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<IReadOnlyList<SmartRecipeDto>>>(ok.Value);
        Assert.Equal(200, response.Code);
        Assert.Equal("Add items to your inventory to get personalized recipes.", response.Message);
    }

    [Fact]
    public async Task GetSmartRecipes_ReturnsBadRequest_WhenNoHousehold()
    {
        var fakeSmartService = new FakeSmartRecipeService
        {
            GetOrGenerateResult = new SmartRecipeResult(SmartRecipeResultStatus.NoHousehold)
        };
        var fakeUserService = new FakeUserService { UserResult = NewUserDto() };
        var controller = CreateController(fakeSmartService, fakeUserService, BuildPrincipal("clerk_123"));

        var result = await controller.GetSmartRecipes(CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<IReadOnlyList<SmartRecipeDto>>>(badRequest.Value);
        Assert.Equal(400, response.Code);
        Assert.Equal("You need to be part of a household.", response.Message);
    }

    [Fact]
    public async Task GetSmartRecipes_ReturnsServerError_WhenGenerationFails()
    {
        var fakeSmartService = new FakeSmartRecipeService
        {
            GetOrGenerateResult = new SmartRecipeResult(SmartRecipeResultStatus.GenerationFailed, ErrorMessage: "AI error")
        };
        var fakeUserService = new FakeUserService { UserResult = NewUserDto() };
        var controller = CreateController(fakeSmartService, fakeUserService, BuildPrincipal("clerk_123"));

        var result = await controller.GetSmartRecipes(CancellationToken.None);

        var serverError = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, serverError.StatusCode);
        var response = Assert.IsType<ApiResponse<IReadOnlyList<SmartRecipeDto>>>(serverError.Value);
        Assert.Equal(500, response.Code);
        Assert.Equal("AI error", response.Message);
    }

    [Fact]
    public async Task GetSmartRecipes_ReturnsOk_WhenSucceeds()
    {
        var recipes = new List<SmartRecipeDto> { NewSmartRecipeDto() };
        var fakeSmartService = new FakeSmartRecipeService
        {
            GetOrGenerateResult = new SmartRecipeResult(SmartRecipeResultStatus.Success, recipes)
        };
        var fakeUserService = new FakeUserService { UserResult = NewUserDto() };
        var controller = CreateController(fakeSmartService, fakeUserService, BuildPrincipal("clerk_123"));

        var result = await controller.GetSmartRecipes(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<IReadOnlyList<SmartRecipeDto>>>(ok.Value);
        Assert.Equal(0, response.Code);
        Assert.NotNull(response.Data);
        Assert.Single(response.Data);
    }

    #endregion

    #region RefreshSmartRecipes Tests

    [Fact]
    public async Task RefreshSmartRecipes_ReturnsUnauthorized_WhenClerkUserIdMissing()
    {
        var fakeSmartService = new FakeSmartRecipeService();
        var fakeUserService = new FakeUserService();
        var controller = CreateController(fakeSmartService, fakeUserService, new ClaimsPrincipal(new ClaimsIdentity()));

        var result = await controller.RefreshSmartRecipes(servings: null, CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<IReadOnlyList<SmartRecipeDto>>>(unauthorized.Value);
        Assert.Equal(401, response.Code);
    }

    [Fact]
    public async Task RefreshSmartRecipes_ReturnsUnauthorized_WhenUserNotFound()
    {
        var fakeSmartService = new FakeSmartRecipeService();
        var fakeUserService = new FakeUserService { UserResult = null };
        var controller = CreateController(fakeSmartService, fakeUserService, BuildPrincipal("clerk_123"));

        var result = await controller.RefreshSmartRecipes(servings: null, CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<IReadOnlyList<SmartRecipeDto>>>(unauthorized.Value);
        Assert.Equal(401, response.Code);
    }

    [Fact]
    public async Task RefreshSmartRecipes_ReturnsOk_WhenSucceeds()
    {
        var recipes = new List<SmartRecipeDto> { NewSmartRecipeDto() };
        var fakeSmartService = new FakeSmartRecipeService
        {
            ForceRegenerateResult = new SmartRecipeResult(SmartRecipeResultStatus.Success, recipes)
        };
        var fakeUserService = new FakeUserService { UserResult = NewUserDto() };
        var controller = CreateController(fakeSmartService, fakeUserService, BuildPrincipal("clerk_123"));

        var result = await controller.RefreshSmartRecipes(servings: 4, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<IReadOnlyList<SmartRecipeDto>>>(ok.Value);
        Assert.Equal(0, response.Code);
        Assert.NotNull(response.Data);
        Assert.Single(response.Data);
        Assert.Equal(4, fakeSmartService.LastServings);
    }

    [Fact]
    public async Task RefreshSmartRecipes_ReturnsBadRequest_WhenNoHousehold()
    {
        var fakeSmartService = new FakeSmartRecipeService
        {
            ForceRegenerateResult = new SmartRecipeResult(SmartRecipeResultStatus.NoHousehold)
        };
        var fakeUserService = new FakeUserService { UserResult = NewUserDto() };
        var controller = CreateController(fakeSmartService, fakeUserService, BuildPrincipal("clerk_123"));

        var result = await controller.RefreshSmartRecipes(servings: null, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<IReadOnlyList<SmartRecipeDto>>>(badRequest.Value);
        Assert.Equal(400, response.Code);
    }

    #endregion

    private static SmartRecipesController CreateController(
        ISmartRecipeService smartService,
        IUserService userService,
        ClaimsPrincipal user) =>
        new(smartService, userService, NullLogger<SmartRecipesController>.Instance)
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

    private static SmartRecipeDto NewSmartRecipeDto() =>
        new(
            Guid.NewGuid(),
            DefaultRecipeId,
            "Smart Recipe",
            "A smart recipe",
            null,
            30,
            RecipeDifficulty.Easy,
            4,
            2,
            ["Salt", "Pepper"],
            0.85m,
            DateOnly.FromDateTime(DateTime.UtcNow),
            DefaultCreatedAt,
            [new SmartRecipeIngredientDto("Test Ingredient", 1, "cup", false, "Vegetables")]
        );

    private sealed class FakeSmartRecipeService : ISmartRecipeService
    {
        public SmartRecipeResult GetOrGenerateResult { get; set; } =
            new(SmartRecipeResultStatus.Success, []);
        public SmartRecipeResult ForceRegenerateResult { get; set; } =
            new(SmartRecipeResultStatus.Success, []);

        public int? LastServings { get; private set; }

        public Task<SmartRecipeResult> GetOrGenerateAsync(Guid userId, bool allowStale = false, CancellationToken cancellationToken = default)
            => Task.FromResult(GetOrGenerateResult);

        public Task<SmartRecipeResult> ForceRegenerateAsync(Guid userId, int? servings = null, CancellationToken cancellationToken = default)
        {
            LastServings = servings;
            return Task.FromResult(ForceRegenerateResult);
        }

        public Task InvalidateForHouseholdAsync(Guid householdId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public async IAsyncEnumerable<SmartRecipeSseEvent> StreamGenerateAsync(
            Guid userId,
            int? servings = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class FakeUserService : IUserService
    {
        public UserResponseDto? UserResult { get; set; }

        public Task<UserResponseDto> GetOrCreateAsync(UserSyncPayload payload, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<UserResponseDto?> GetByClerkUserIdAsync(string clerkUserId, CancellationToken cancellationToken = default)
            => Task.FromResult(UserResult);

        public Task<UserProfileResponseDto?> GetProfileAsync(string clerkUserId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<UserProfileResponseDto?> UpdateProfileAsync(Guid userId, UpdateUserProfileRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}
