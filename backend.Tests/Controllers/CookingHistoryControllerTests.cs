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

public class CookingHistoryControllerTests
{
    private static readonly Guid DefaultRecipeId = Guid.Parse("00000000-0000-0000-0000-000000000011");
    private static readonly Guid DefaultCookId = Guid.Parse("00000000-0000-0000-0000-000000000022");
    private static readonly DateTime DefaultCookDate = new(2024, 01, 01, 0, 0, 0, DateTimeKind.Utc);

    #region RecordCookComplete Tests

    [Fact]
    public async Task RecordCookComplete_ReturnsUnauthorized_WhenClerkUserIdMissing()
    {
        var fakeService = new FakeRecipeCookService();
        var controller = CreateController(fakeService, new ClaimsPrincipal(new ClaimsIdentity()));

        var result = await controller.RecordCookComplete(DefaultRecipeId, CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<RecipeCookResponseDto>>(unauthorized.Value);
        Assert.Equal(401, response.Code);
        Assert.Null(fakeService.LastClerkUserId);
    }

    [Fact]
    public async Task RecordCookComplete_ReturnsNotFound_WhenServiceReturnsNull()
    {
        var fakeService = new FakeRecipeCookService { RecordCookResult = null };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));

        var result = await controller.RecordCookComplete(DefaultRecipeId, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<RecipeCookResponseDto>>(notFound.Value);
        Assert.Equal(404, response.Code);
        Assert.Equal("User or recipe not found.", response.Message);
    }

    [Fact]
    public async Task RecordCookComplete_ReturnsOk_WhenSucceeds()
    {
        var cookResponse = new RecipeCookResponseDto(DefaultRecipeId, DefaultCookId, 5, DefaultCookDate);
        var fakeService = new FakeRecipeCookService { RecordCookResult = cookResponse };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));

        var result = await controller.RecordCookComplete(DefaultRecipeId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<RecipeCookResponseDto>>(ok.Value);
        Assert.Equal(0, response.Code);
        Assert.NotNull(response.Data);
        Assert.Equal(DefaultRecipeId, response.Data.RecipeId);
        Assert.Equal(5, response.Data.CookCount);
        Assert.Equal("clerk_123", fakeService.LastClerkUserId);
        Assert.Equal(DefaultRecipeId, fakeService.LastRecipeId);
    }

    #endregion

    #region GetMyCookedRecipes Tests

    [Fact]
    public async Task GetMyCookedRecipes_ReturnsUnauthorized_WhenClerkUserIdMissing()
    {
        var fakeService = new FakeRecipeCookService();
        var controller = CreateController(fakeService, new ClaimsPrincipal(new ClaimsIdentity()));

        var result = await controller.GetMyCookedRecipes(cancellationToken: CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<IReadOnlyList<MyCookedRecipeCardDto>>>(unauthorized.Value);
        Assert.Equal(401, response.Code);
    }

    [Fact]
    public async Task GetMyCookedRecipes_ReturnsUnauthorized_WhenServiceReturnsNull()
    {
        var fakeService = new FakeRecipeCookService { CookedRecipesResult = null };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));

        var result = await controller.GetMyCookedRecipes(cancellationToken: CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<IReadOnlyList<MyCookedRecipeCardDto>>>(unauthorized.Value);
        Assert.Equal(401, response.Code);
        Assert.Equal("User not found.", response.Message);
    }

    [Fact]
    public async Task GetMyCookedRecipes_ReturnsOk_WhenSucceeds()
    {
        var recipes = new List<MyCookedRecipeCardDto>
        {
            new(DefaultCookId, DefaultRecipeId, "Test Recipe", null, null, null, "Author", 2, DefaultCookDate, DefaultCookDate)
        };
        var fakeService = new FakeRecipeCookService { CookedRecipesResult = recipes };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));

        var result = await controller.GetMyCookedRecipes(page: 1, pageSize: 20, cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<IReadOnlyList<MyCookedRecipeCardDto>>>(ok.Value);
        Assert.Equal(0, response.Code);
        Assert.NotNull(response.Data);
        Assert.Single(response.Data);
        Assert.Equal("clerk_123", fakeService.LastClerkUserId);
    }

    #endregion

    #region GetMyCooksCount Tests

    [Fact]
    public async Task GetMyCooksCount_ReturnsUnauthorized_WhenClerkUserIdMissing()
    {
        var fakeService = new FakeRecipeCookService();
        var controller = CreateController(fakeService, new ClaimsPrincipal(new ClaimsIdentity()));

        var result = await controller.GetMyCooksCount(CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<MeCooksCountDto>>(unauthorized.Value);
        Assert.Equal(401, response.Code);
    }

    [Fact]
    public async Task GetMyCooksCount_ReturnsUnauthorized_WhenServiceReturnsNull()
    {
        var fakeService = new FakeRecipeCookService { CooksCountResult = null };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));

        var result = await controller.GetMyCooksCount(CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<MeCooksCountDto>>(unauthorized.Value);
        Assert.Equal(401, response.Code);
        Assert.Equal("User not found.", response.Message);
    }

    [Fact]
    public async Task GetMyCooksCount_ReturnsOk_WhenSucceeds()
    {
        var fakeService = new FakeRecipeCookService { CooksCountResult = 10 };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));

        var result = await controller.GetMyCooksCount(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<MeCooksCountDto>>(ok.Value);
        Assert.Equal(0, response.Code);
        Assert.NotNull(response.Data);
        Assert.Equal(10, response.Data.Count);
    }

    #endregion

    #region DeleteCookEntry Tests

    [Fact]
    public async Task DeleteCookEntry_ReturnsUnauthorized_WhenClerkUserIdMissing()
    {
        var fakeService = new FakeRecipeCookService();
        var controller = CreateController(fakeService, new ClaimsPrincipal(new ClaimsIdentity()));

        var result = await controller.DeleteCookEntry(DefaultCookId, CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<object>>(unauthorized.Value);
        Assert.Equal(401, response.Code);
    }

    [Fact]
    public async Task DeleteCookEntry_ReturnsNotFound_WhenServiceReturnsFalse()
    {
        var fakeService = new FakeRecipeCookService { DeleteEntryResult = false };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));

        var result = await controller.DeleteCookEntry(DefaultCookId, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<object>>(notFound.Value);
        Assert.Equal(404, response.Code);
        Assert.Equal("Cook entry not found or access denied.", response.Message);
    }

    [Fact]
    public async Task DeleteCookEntry_ReturnsOk_WhenSucceeds()
    {
        var fakeService = new FakeRecipeCookService { DeleteEntryResult = true };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));

        var result = await controller.DeleteCookEntry(DefaultCookId, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal("clerk_123", fakeService.LastClerkUserId);
        Assert.Equal(DefaultCookId, fakeService.LastCookId);
    }

    #endregion

    #region ClearAllCookHistory Tests

    [Fact]
    public async Task ClearAllCookHistory_ReturnsUnauthorized_WhenClerkUserIdMissing()
    {
        var fakeService = new FakeRecipeCookService();
        var controller = CreateController(fakeService, new ClaimsPrincipal(new ClaimsIdentity()));

        var result = await controller.ClearAllCookHistory(CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<object>>(unauthorized.Value);
        Assert.Equal(401, response.Code);
    }

    [Fact]
    public async Task ClearAllCookHistory_ReturnsUnauthorized_WhenServiceReturnsFalse()
    {
        var fakeService = new FakeRecipeCookService { ClearAllResult = false };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));

        var result = await controller.ClearAllCookHistory(CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<object>>(unauthorized.Value);
        Assert.Equal(401, response.Code);
        Assert.Equal("User not found.", response.Message);
    }

    [Fact]
    public async Task ClearAllCookHistory_ReturnsOk_WhenSucceeds()
    {
        var fakeService = new FakeRecipeCookService { ClearAllResult = true };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));

        var result = await controller.ClearAllCookHistory(CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal("clerk_123", fakeService.LastClerkUserId);
    }

    #endregion

    private static CookingHistoryController CreateController(IRecipeCookService service, ClaimsPrincipal user) =>
        new(service, NullLogger<CookingHistoryController>.Instance)
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

    private sealed class FakeRecipeCookService : IRecipeCookService
    {
        public RecipeCookResponseDto? RecordCookResult { get; set; }
        public IReadOnlyList<MyCookedRecipeCardDto>? CookedRecipesResult { get; set; } = [];
        public int? CooksCountResult { get; set; } = 0;
        public bool DeleteEntryResult { get; set; } = true;
        public bool ClearAllResult { get; set; } = true;

        public string? LastClerkUserId { get; private set; }
        public Guid? LastRecipeId { get; private set; }
        public Guid? LastCookId { get; private set; }

        public Task<RecipeCookResponseDto?> RecordCookAsync(Guid recipeId, string clerkUserId, CancellationToken cancellationToken = default)
        {
            LastClerkUserId = clerkUserId;
            LastRecipeId = recipeId;
            return Task.FromResult(RecordCookResult);
        }

        public Task<IReadOnlyList<MyCookedRecipeCardDto>?> GetMyCookedRecipesAsync(string clerkUserId, int page, int pageSize, string? searchQuery, CancellationToken cancellationToken = default)
        {
            LastClerkUserId = clerkUserId;
            return Task.FromResult(CookedRecipesResult);
        }

        public Task<int?> GetMyCooksCountAsync(string clerkUserId, CancellationToken cancellationToken = default)
        {
            LastClerkUserId = clerkUserId;
            return Task.FromResult(CooksCountResult);
        }

        public Task<bool> DeleteCookEntryAsync(Guid cookId, string clerkUserId, CancellationToken cancellationToken = default)
        {
            LastClerkUserId = clerkUserId;
            LastCookId = cookId;
            return Task.FromResult(DeleteEntryResult);
        }

        public Task<bool> ClearAllCookHistoryAsync(string clerkUserId, CancellationToken cancellationToken = default)
        {
            LastClerkUserId = clerkUserId;
            return Task.FromResult(ClearAllResult);
        }
    }
}
