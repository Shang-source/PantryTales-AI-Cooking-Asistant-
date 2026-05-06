using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
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

public class RecipesControllerTests
{
    private static readonly Guid RecipeId = Guid.Parse("00000000-0000-0000-0000-000000000333");
    private static readonly Guid AuthorId = Guid.Parse("00000000-0000-0000-0000-000000000555");

    [Fact]
    public async Task CreateAsync_ReturnsUnauthorized_WhenClerkIdMissing()
    {
        var controller = CreateController(new FakeRecipeService(), new ClaimsPrincipal(new ClaimsIdentity()));
        var request = NewRequest();

        var result = await controller.CreateAsync(request, CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<RecipeDetailDto>>(unauthorized.Value);
        Assert.Equal(401, api.Code);
        Assert.Equal("Could not determine Clerk user id from token.", api.Message);
        Assert.Null(api.Data);
    }

    [Theory]
    [InlineData(CreateRecipeResultStatus.UserNotFound, typeof(UnauthorizedObjectResult), 401, "User not found.")]
    [InlineData(CreateRecipeResultStatus.HouseholdNotFound, typeof(BadRequestObjectResult), 400, "No household found for current user.")]
    [InlineData(CreateRecipeResultStatus.InvalidRequest, typeof(BadRequestObjectResult), 400, "Invalid recipe payload.")]
    [InlineData(CreateRecipeResultStatus.Failed, typeof(ObjectResult), 500, "Failed to create recipe.")]
    public async Task CreateAsync_ReturnsExpectedEnvelope_ForNonSuccessStatus(
        CreateRecipeResultStatus status,
        Type expectedActionResultType,
        int expectedCode,
        string expectedMessage)
    {
        var fakeService = new FakeRecipeService
        {
            Result = new CreateRecipeResult(status, null)
        };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));
        var request = NewRequest();

        var result = await controller.CreateAsync(request, CancellationToken.None);

        Assert.Equal(request, fakeService.LastRequest);
        Assert.Equal("clerk_123", fakeService.LastClerkUserId);
        var actionResult = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        Assert.IsType(expectedActionResultType, actionResult);
        var api = Assert.IsType<ApiResponse<RecipeDetailDto>>(actionResult.Value);
        Assert.Equal(expectedCode, api.Code);
        Assert.Equal(expectedMessage, api.Message);
        Assert.Null(api.Data);
    }

    [Fact]
    public async Task CreateAsync_ReturnsOk_WhenServiceSucceeds()
    {
        var detail = NewDetail();
        var fakeService = new FakeRecipeService
        {
            Result = new CreateRecipeResult(CreateRecipeResultStatus.Success, detail)
        };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_abc"));
        var request = NewRequest();

        var result = await controller.CreateAsync(request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<RecipeDetailDto>>(ok.Value);
        Assert.Equal(0, api.Code);
        Assert.Equal("Ok", api.Message);
        Assert.Same(detail, api.Data);
        Assert.Equal(request, fakeService.LastRequest);
        Assert.Equal("clerk_abc", fakeService.LastClerkUserId);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsOk_WhenServiceSucceeds()
    {
        var detail = NewDetail();
        var fakeService = new FakeRecipeService
        {
            DetailResult = new RecipeDetailResult(RecipeDetailResultStatus.Success, detail)
        };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_ok"));

        var result = await controller.GetByIdAsync(detail.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<RecipeDetailDto>>(ok.Value);
        Assert.Equal(0, api.Code);
        Assert.Same(detail, api.Data);
        Assert.Equal(detail.Id, fakeService.LastDetailRecipeId);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNotFound_WhenRecipeMissing()
    {
        var fakeService = new FakeRecipeService
        {
            DetailResult = new RecipeDetailResult(RecipeDetailResultStatus.RecipeNotFound, null, "Recipe not found.")
        };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_missing"));

        var result = await controller.GetByIdAsync(RecipeId, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<RecipeDetailDto>>(notFound.Value);
        Assert.Equal(404, api.Code);
        Assert.Equal("Recipe not found.", api.Message);
        Assert.Null(api.Data);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsUnauthorized_WhenClerkIdMissing()
    {
        var controller = CreateController(new FakeRecipeService(), new ClaimsPrincipal(new ClaimsIdentity()));
        var request = NewRequest();

        var result = await controller.UpdateAsync(RecipeId, request, CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<RecipeDetailDto>>(unauthorized.Value);
        Assert.Equal(401, api.Code);
        Assert.Equal("Could not determine Clerk user id from token.", api.Message);
        Assert.Null(api.Data);
    }

    [Theory]
    [InlineData(UpdateRecipeResultStatus.UserNotFound, typeof(UnauthorizedObjectResult), 401, "User not found.")]
    [InlineData(UpdateRecipeResultStatus.RecipeNotFound, typeof(NotFoundObjectResult), 404, "Recipe not found.")]
    [InlineData(UpdateRecipeResultStatus.Unauthorized, typeof(ObjectResult), 403, "Not authorized to update this recipe.")]
    [InlineData(UpdateRecipeResultStatus.InvalidRequest, typeof(BadRequestObjectResult), 400, "Invalid recipe payload.")]
    [InlineData(UpdateRecipeResultStatus.Failed, typeof(ObjectResult), 500, "Failed to update recipe.")]
    public async Task UpdateAsync_ReturnsExpectedEnvelope_ForNonSuccessStatus(
        UpdateRecipeResultStatus status,
        Type expectedActionResultType,
        int expectedCode,
        string expectedMessage)
    {
        var fakeService = new FakeRecipeService
        {
            UpdateResult = new UpdateRecipeResult(status, null, expectedMessage)
        };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_update"));
        var request = NewRequest();

        var result = await controller.UpdateAsync(RecipeId, request, CancellationToken.None);

        Assert.Equal(RecipeId, fakeService.LastUpdateRecipeId);
        Assert.Equal(request, fakeService.LastUpdateRequest);
        Assert.Equal("clerk_update", fakeService.LastClerkUserId);
        var actionResult = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        Assert.IsType(expectedActionResultType, actionResult);
        var api = Assert.IsType<ApiResponse<RecipeDetailDto>>(actionResult.Value);
        Assert.Equal(expectedCode, api.Code);
        Assert.Equal(expectedMessage, api.Message);
        Assert.Null(api.Data);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsOk_WhenServiceSucceeds()
    {
        var detail = NewDetail();
        var fakeService = new FakeRecipeService
        {
            UpdateResult = new UpdateRecipeResult(UpdateRecipeResultStatus.Success, detail)
        };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_good"));
        var request = NewRequest();

        var result = await controller.UpdateAsync(detail.Id, request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<RecipeDetailDto>>(ok.Value);
        Assert.Equal(0, api.Code);
        Assert.Same(detail, api.Data);
        Assert.Equal(detail.Id, fakeService.LastUpdateRecipeId);
        Assert.Equal(request, fakeService.LastUpdateRequest);
        Assert.Equal("clerk_good", fakeService.LastClerkUserId);
    }

    #region Featured Recipes Tests

    [Fact]
    public async Task GetFeaturedAsync_ReturnsBadRequest_WhenCountLessThanOne()
    {
        var controller = CreateController(new FakeRecipeService(), new ClaimsPrincipal());

        var result = await controller.GetFeaturedAsync(0, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<List<FeaturedRecipeDto>>>(badRequest.Value);
        Assert.Equal(400, api.Code);
        Assert.Equal("Count must be between 1 and 20.", api.Message);
    }

    [Fact]
    public async Task GetFeaturedAsync_ReturnsBadRequest_WhenCountGreaterThanTwenty()
    {
        var controller = CreateController(new FakeRecipeService(), new ClaimsPrincipal());

        var result = await controller.GetFeaturedAsync(21, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<List<FeaturedRecipeDto>>>(badRequest.Value);
        Assert.Equal(400, api.Code);
        Assert.Equal("Count must be between 1 and 20.", api.Message);
    }

    [Fact]
    public async Task GetFeaturedAsync_ReturnsEmptyList_WhenNoFeaturedRecipes()
    {
        var fakeRepo = new FakeRecipeRepository { FeaturedRecipes = new List<Recipe>() };
        var controller = CreateController(new FakeRecipeService(), new ClaimsPrincipal(), fakeRepo);

        var result = await controller.GetFeaturedAsync(10, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<List<FeaturedRecipeDto>>>(ok.Value);
        Assert.Equal(0, api.Code);
        Assert.Empty(api.Data!);
        Assert.Equal("No featured recipes available.", api.Message);
    }

    [Fact]
    public async Task GetFeaturedAsync_ReturnsFeaturedRecipes()
    {
        var author = new User { Id = AuthorId, Nickname = "Chef", AvatarUrl = "https://example.com/avatar.png" };
        var recipe = new Recipe
        {
            Id = RecipeId,
            Title = "Featured Recipe",
            Description = "A great recipe",
            ImageUrls = new List<string> { "https://example.com/image.jpg" },
            TotalTimeMinutes = 30,
            Difficulty = RecipeDifficulty.Easy,
            Servings = 4,
            LikesCount = 10,
            SavedCount = 5,
            AuthorId = AuthorId,
            Author = author,
            IsFeatured = true,
            Visibility = RecipeVisibility.Public,
            Type = RecipeType.User,
            Tags = new List<RecipeTag>()
        };
        var fakeRepo = new FakeRecipeRepository { FeaturedRecipes = new List<Recipe> { recipe } };
        var controller = CreateController(new FakeRecipeService(), new ClaimsPrincipal(), fakeRepo);

        var result = await controller.GetFeaturedAsync(10, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<List<FeaturedRecipeDto>>>(ok.Value);
        Assert.Equal(0, api.Code);
        Assert.Single(api.Data!);
        var dto = api.Data![0];
        Assert.Equal(RecipeId, dto.Id);
        Assert.Equal("Featured Recipe", dto.Title);
        Assert.Equal("Chef", dto.AuthorNickname);
        Assert.Equal(10, dto.LikesCount);
        Assert.Equal(5, dto.SavedCount);
    }

    #endregion

    private static RecipesController CreateController(IRecipeService service, ClaimsPrincipal user, IRecipeRepository? repository = null)
    {
        return new RecipesController(service, repository ?? new FakeRecipeRepository(), NullLogger<RecipesController>.Instance)
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

    private static CreateRecipeRequestDto NewRequest() =>
        new()
        {
            Title = "Test Recipe",
            Description = "desc",
            Steps = ["step 1"],
            Visibility = RecipeVisibility.Public
        };

    private static RecipeDetailDto NewDetail() =>
        new(
            Id: RecipeId,
            HouseholdId: Guid.Parse("00000000-0000-0000-0000-000000000444"),
            AuthorId: Guid.Parse("00000000-0000-0000-0000-000000000555"),
            Author: new RecipeAuthorDto(
                Guid.Parse("00000000-0000-0000-0000-000000000555"),
                "Tester",
                "https://example.com/avatar.png"),
            Title: "Test Recipe",
            Description: "desc",
            Steps: new List<string> { "step 1" },
            Visibility: RecipeVisibility.Public,
            Type: RecipeType.User,
            ImageUrls: new List<string> { "img" },
            LikesCount: 0,
            LikedByMe: false,
            CommentsCount: 0,
            SavedCount: 0,
            SavedByMe: false,
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow,
            Ingredients: new List<RecipeIngredientDto>(),
            Tags: new List<string> { "tag1" },
            Servings: 2,
            TotalTimeMinutes: 10,
            Difficulty: RecipeDifficulty.Easy);

    private sealed class FakeRecipeService : IRecipeService
    {
        public CreateRecipeResult Result { get; set; } = new(CreateRecipeResultStatus.Failed, null);
        public RecipeDetailResult DetailResult { get; set; } =
            new(RecipeDetailResultStatus.RecipeNotFound, null, "Recipe not found.");
        public UpdateRecipeResult UpdateResult { get; set; } =
            new(UpdateRecipeResultStatus.Failed, null, "Failed to update recipe.");
        public CreateRecipeRequestDto? LastRequest { get; private set; }
        public string? LastClerkUserId { get; private set; }
        public Guid? LastDetailRecipeId { get; private set; }
        public Guid? LastUpdateRecipeId { get; private set; }
        public CreateRecipeRequestDto? LastUpdateRequest { get; private set; }

        public Task<CreateRecipeResult> CreateAsync(CreateRecipeRequestDto request, string clerkUserId,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            LastClerkUserId = clerkUserId;
            return Task.FromResult(Result);
        }

        public Task<RecipeListResult> ListAsync(string? scope, ClaimsPrincipal user, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RecipeListResult(RecipeListResultStatus.InvalidScope, Array.Empty<RecipeCardDto>(), "not implemented"));

        public Task<RecipeDetailResult> GetByIdAsync(Guid recipeId, ClaimsPrincipal user,
            CancellationToken cancellationToken = default)
        {
            LastDetailRecipeId = recipeId;
            return Task.FromResult(DetailResult);
        }

        public Task<DeleteRecipeResult> DeleteAsync(Guid recipeId, string clerkUserId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new DeleteRecipeResult(DeleteRecipeResultStatus.Success));

        public Task<UpdateRecipeResult> UpdateAsync(Guid recipeId, CreateRecipeRequestDto request, string clerkUserId,
            CancellationToken cancellationToken = default)
        {
            LastUpdateRecipeId = recipeId;
            LastUpdateRequest = request;
            LastClerkUserId = clerkUserId;
            return Task.FromResult(UpdateResult);
        }
    }

    private sealed class FakeRecipeRepository : IRecipeRepository
    {
        public List<Recipe> FeaturedRecipes { get; set; } = new();
        public Recipe? RecipeToReturn { get; set; }
        public int LastFeaturedCount { get; private set; }
        public Guid? LastToggledRecipeId { get; private set; }

        public Task<Recipe?> GetByIdAsync(Guid recipeId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(RecipeToReturn);
        }

        public Task<Recipe?> GetDetailedByIdAsync(Guid recipeId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(RecipeToReturn);
        }

        public Task<List<Recipe>> GetFeaturedRecipesAsync(int count, CancellationToken cancellationToken = default)
        {
            LastFeaturedCount = count;
            return Task.FromResult(FeaturedRecipes.Take(count).ToList());
        }

        public Task ToggleFeaturedAsync(Guid recipeId, CancellationToken cancellationToken = default)
        {
            LastToggledRecipeId = recipeId;
            return Task.CompletedTask;
        }
    }
}
