using System;
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

public class RecipeCommentsControllerTests
{
    private static readonly Guid RecipeId = Guid.Parse("00000000-0000-0000-0000-000000000123");
    private static readonly Guid CommentId = Guid.Parse("00000000-0000-0000-0000-000000000456");

    [Fact]
    public async Task GetRecipeCommentsAsync_ReturnsNotFound_WhenServiceReturnsNull()
    {
        var fakeService = new FakeRecipeCommentService { CommentsResponse = null };
        var controller = CreateController(fakeService, new ClaimsPrincipal(new ClaimsIdentity()));

        var result = await controller.GetRecipeCommentsAsync(RecipeId, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<CommentListResponseDto>>(notFound.Value);
        Assert.Equal(404, api.Code);
        Assert.Equal("Recipe not found.", api.Message);
        Assert.Null(api.Data);
        Assert.Equal(RecipeId, fakeService.LastRecipeId);
        Assert.Null(fakeService.LastClerkUserId);
    }

    [Fact]
    public async Task GetRecipeCommentsAsync_ReturnsOk_WhenServiceReturnsResponse()
    {
        var dto = new CommentListResponseDto(RecipeId, TotalCount: 0, Items: []);
        var fakeService = new FakeRecipeCommentService { CommentsResponse = dto };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_abc"));

        var result = await controller.GetRecipeCommentsAsync(RecipeId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<CommentListResponseDto>>(ok.Value);
        Assert.Equal(0, api.Code);
        Assert.Equal("Ok", api.Message);
        Assert.Same(dto, api.Data);
        Assert.Equal(RecipeId, fakeService.LastRecipeId);
        Assert.Equal("clerk_abc", fakeService.LastClerkUserId);
    }

    [Fact]
    public async Task CreateRecipeCommentAsync_ReturnsUnauthorized_WhenClerkIdMissing()
    {
        var controller = CreateController(new FakeRecipeCommentService(), new ClaimsPrincipal(new ClaimsIdentity()));

        var result = await controller.CreateRecipeCommentAsync(
            RecipeId,
            new CreateCommentRequestDto("hello"),
            CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<CommentDto>>(unauthorized.Value);
        Assert.Equal(401, api.Code);
        Assert.Equal("Could not determine Clerk user id from token.", api.Message);
        Assert.Null(api.Data);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateRecipeCommentAsync_ReturnsBadRequest_WhenContentMissing(string? content)
    {
        var controller = CreateController(new FakeRecipeCommentService(), BuildPrincipal("clerk_ok"));

        var result = await controller.CreateRecipeCommentAsync(
            RecipeId,
            new CreateCommentRequestDto(content ?? string.Empty),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<CommentDto>>(badRequest.Value);
        Assert.Equal(400, api.Code);
        Assert.Equal("Content is required.", api.Message);
        Assert.Null(api.Data);
    }

    [Fact]
    public async Task CreateRecipeCommentAsync_ReturnsBadRequest_WhenContentTooLong()
    {
        var controller = CreateController(new FakeRecipeCommentService(), BuildPrincipal("clerk_ok"));
        var content = new string('a', 1001);

        var result = await controller.CreateRecipeCommentAsync(
            RecipeId,
            new CreateCommentRequestDto(content),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<CommentDto>>(badRequest.Value);
        Assert.Equal(400, api.Code);
        Assert.Equal("Content must be 1000 characters or less.", api.Message);
        Assert.Null(api.Data);
    }

    [Fact]
    public async Task CreateRecipeCommentAsync_ReturnsOk_WhenServiceSucceeds()
    {
        var created = new CommentDto(
            CommentId,
            RecipeId,
            Guid.Parse("00000000-0000-0000-0000-000000000789"),
            "Alice",
            null,
            "Hello",
            DateTime.UtcNow,
            CanDelete: true);
        var fakeService = new FakeRecipeCommentService
        {
            CreateResult = new CreateCommentResult(CreateCommentResultStatus.Success, created)
        };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_abc"));

        var result = await controller.CreateRecipeCommentAsync(
            RecipeId,
            new CreateCommentRequestDto("Hello"),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<CommentDto>>(ok.Value);
        Assert.Equal(0, api.Code);
        Assert.Equal("Ok", api.Message);
        Assert.Same(created, api.Data);
        Assert.Equal(RecipeId, fakeService.LastRecipeId);
        Assert.Equal("clerk_abc", fakeService.LastClerkUserId);
        Assert.Equal("Hello", fakeService.LastContent);
    }

    [Theory]
    [InlineData(CreateCommentResultStatus.UserNotFound, typeof(UnauthorizedObjectResult), 401, "User not found.")]
    [InlineData(CreateCommentResultStatus.RecipeNotFound, typeof(NotFoundObjectResult), 404, "Recipe not found.")]
    public async Task CreateRecipeCommentAsync_ReturnsExpectedEnvelope_ForNonSuccessStatus(
        CreateCommentResultStatus status,
        Type expectedActionResultType,
        int expectedCode,
        string expectedMessage)
    {
        var fakeService = new FakeRecipeCommentService
        {
            CreateResult = new CreateCommentResult(status)
        };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_abc"));

        var result = await controller.CreateRecipeCommentAsync(
            RecipeId,
            new CreateCommentRequestDto("Hello"),
            CancellationToken.None);

        var actionResult = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        Assert.IsType(expectedActionResultType, actionResult);
        var api = Assert.IsType<ApiResponse<CommentDto>>(actionResult.Value);
        Assert.Equal(expectedCode, api.Code);
        Assert.Equal(expectedMessage, api.Message);
        Assert.Null(api.Data);
    }

    [Fact]
    public async Task DeleteCommentAsync_ReturnsUnauthorized_WhenClerkIdMissing()
    {
        var controller = CreateController(new FakeRecipeCommentService(), new ClaimsPrincipal(new ClaimsIdentity()));

        var result = await controller.DeleteCommentAsync(CommentId, CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        var api = Assert.IsType<ApiResponse>(unauthorized.Value);
        Assert.Equal(401, api.Code);
        Assert.Equal("Could not determine Clerk user id from token.", api.Message);
    }

    [Fact]
    public async Task DeleteCommentAsync_ReturnsOk_WithEnvelope_WhenDeleted()
    {
        var fakeService = new FakeRecipeCommentService { DeleteResult = DeleteCommentResult.Deleted };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_abc"));

        var result = await controller.DeleteCommentAsync(CommentId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var api = Assert.IsType<ApiResponse<CommentDeleteResponseDto>>(ok.Value);
        Assert.Equal(0, api.Code);
        Assert.Equal("Deleted successfully", api.Message);
        Assert.Equal(CommentId, api.Data?.CommentId);
        Assert.True(api.Data?.Deleted);
        Assert.Equal("clerk_abc", fakeService.LastClerkUserId);
        Assert.Equal(CommentId, fakeService.LastCommentId);
    }

    [Theory]
    [InlineData(DeleteCommentResult.CommentNotFound, typeof(NotFoundObjectResult), 404, "Comment not found.")]
    [InlineData(DeleteCommentResult.UserNotFound, typeof(UnauthorizedObjectResult), 401, "User not found.")]
    public async Task DeleteCommentAsync_ReturnsExpectedEnvelope_ForNotDeletedStatus(
        DeleteCommentResult status,
        Type expectedActionResultType,
        int expectedCode,
        string expectedMessage)
    {
        var fakeService = new FakeRecipeCommentService { DeleteResult = status };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_abc"));

        var result = await controller.DeleteCommentAsync(CommentId, CancellationToken.None);

        var actionResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.IsType(expectedActionResultType, actionResult);
        var api = Assert.IsType<ApiResponse>(actionResult.Value);
        Assert.Equal(expectedCode, api.Code);
        Assert.Equal(expectedMessage, api.Message);
    }

    [Fact]
    public async Task DeleteCommentAsync_ReturnsForbidden_WhenServiceReturnsForbidden()
    {
        var fakeService = new FakeRecipeCommentService { DeleteResult = DeleteCommentResult.Forbidden };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_abc"));

        var result = await controller.DeleteCommentAsync(CommentId, CancellationToken.None);

        var actionResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, actionResult.StatusCode);
        var api = Assert.IsType<ApiResponse>(actionResult.Value);
        Assert.Equal(403, api.Code);
        Assert.Equal("Not authorized to delete this comment.", api.Message);
    }

    [Fact]
    public async Task ToggleLikeAsync_ReturnsUnauthorized_WhenClerkIdMissing()
    {
        var controller = CreateController(new FakeRecipeCommentService(), new ClaimsPrincipal(new ClaimsIdentity()));

        var result = await controller.ToggleLikeAsync(CommentId, CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<ToggleCommentLikeResponseDto>>(unauthorized.Value);
        Assert.Equal(401, api.Code);
        Assert.Equal("Could not determine Clerk user id from token.", api.Message);
        Assert.Null(api.Data);
    }

    [Fact]
    public async Task ToggleLikeAsync_ReturnsOk_WhenServiceSucceeds()
    {
        var fakeService = new FakeRecipeCommentService
        {
            ToggleLikeResult = new ToggleLikeResult(
                ToggleLikeResultStatus.Success,
                new ToggleCommentLikeResponseDto(CommentId, true, 5))
        };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_abc"));

        var result = await controller.ToggleLikeAsync(CommentId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var api = Assert.IsType<ApiResponse<ToggleCommentLikeResponseDto>>(ok.Value);
        Assert.Equal(0, api.Code);
        Assert.Equal("Ok", api.Message);
        Assert.NotNull(api.Data);
        Assert.Equal(CommentId, api.Data.CommentId);
        Assert.True(api.Data.IsLiked);
        Assert.Equal(5, api.Data.LikeCount);
        Assert.Equal(CommentId, fakeService.LastCommentId);
        Assert.Equal("clerk_abc", fakeService.LastClerkUserId);
    }

    [Theory]
    [InlineData(ToggleLikeResultStatus.UserNotFound, typeof(UnauthorizedObjectResult), 401, "User not found.")]
    [InlineData(ToggleLikeResultStatus.CommentNotFound, typeof(NotFoundObjectResult), 404, "Comment not found.")]
    public async Task ToggleLikeAsync_ReturnsExpectedEnvelope_ForNonSuccessStatus(
        ToggleLikeResultStatus status,
        Type expectedActionResultType,
        int expectedCode,
        string expectedMessage)
    {
        var fakeService = new FakeRecipeCommentService
        {
            ToggleLikeResult = new ToggleLikeResult(status)
        };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_abc"));

        var result = await controller.ToggleLikeAsync(CommentId, CancellationToken.None);

        var actionResult = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        Assert.IsType(expectedActionResultType, actionResult);
        var api = Assert.IsType<ApiResponse<ToggleCommentLikeResponseDto>>(actionResult.Value);
        Assert.Equal(expectedCode, api.Code);
        Assert.Equal(expectedMessage, api.Message);
        Assert.Null(api.Data);
    }

    private static RecipeCommentsController CreateController(IRecipeCommentService service, ClaimsPrincipal user)
    {
        return new RecipeCommentsController(service, NullLogger<RecipeCommentsController>.Instance)
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

    private sealed class FakeRecipeCommentService : IRecipeCommentService
    {
        public CommentListResponseDto? CommentsResponse { get; set; } = new(RecipeId, 0, []);
        public CreateCommentResult CreateResult { get; set; } = new(CreateCommentResultStatus.Success, null);
        public DeleteCommentResult DeleteResult { get; set; } = DeleteCommentResult.Deleted;
        public ToggleLikeResult ToggleLikeResult { get; set; } = new(ToggleLikeResultStatus.Success,
            new ToggleCommentLikeResponseDto(CommentId, true, 1));

        public Guid? LastRecipeId { get; private set; }
        public string? LastClerkUserId { get; private set; }
        public string? LastContent { get; private set; }
        public Guid? LastCommentId { get; private set; }

        public Task<CommentListResponseDto?> GetRecipeCommentsAsync(Guid recipeId, string? clerkUserId,
            CancellationToken cancellationToken = default)
        {
            LastRecipeId = recipeId;
            LastClerkUserId = clerkUserId;
            return Task.FromResult(CommentsResponse);
        }

        public Task<CreateCommentResult> CreateRecipeCommentAsync(Guid recipeId, string clerkUserId, string content,
            CancellationToken cancellationToken = default)
        {
            LastRecipeId = recipeId;
            LastClerkUserId = clerkUserId;
            LastContent = content;
            return Task.FromResult(CreateResult);
        }

        public Task<DeleteCommentResult> DeleteCommentAsync(Guid commentId, string clerkUserId,
            CancellationToken cancellationToken = default)
        {
            LastCommentId = commentId;
            LastClerkUserId = clerkUserId;
            return Task.FromResult(DeleteResult);
        }

        public Task<ToggleLikeResult> ToggleLikeAsync(Guid commentId, string clerkUserId,
            CancellationToken cancellationToken = default)
        {
            LastCommentId = commentId;
            LastClerkUserId = clerkUserId;
            return Task.FromResult(ToggleLikeResult);
        }
    }
}
