using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using backend.Controllers;
using backend.Dtos;
using backend.Dtos.Checklist;
using backend.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace backend.Tests.Controllers;

public class ChecklistControllerTests
{
    private static readonly Guid DefaultItemId = Guid.Parse("00000000-0000-0000-0000-000000000011");
    private static readonly DateTime DefaultCreatedAt = new(2024, 01, 01, 0, 0, 0, DateTimeKind.Utc);

    #region ClearAll Tests

    [Fact]
    public async Task ClearAll_ReturnsUnauthorized_WhenClerkUserIdMissing()
    {
        var fakeService = new FakeChecklistService();
        var controller = CreateController(fakeService, new ClaimsPrincipal(new ClaimsIdentity()));

        var result = await controller.ClearAll(CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<int>>(unauthorized.Value);
        Assert.Equal(401, response.Code);
        Assert.Equal("Could not determine user.", response.Message);
        Assert.Null(fakeService.LastClerkUserId);
    }

    [Fact]
    public async Task ClearAll_ReturnsBadRequest_WhenNoActiveHousehold()
    {
        var fakeService = new FakeChecklistService
        {
            ClearAllResultToReturn = ChecklistResult<int>.Fail(ChecklistError.NoActiveHousehold)
        };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));

        var result = await controller.ClearAll(CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<int>>(badRequest.Value);
        Assert.Equal(400, response.Code);
        Assert.Equal("No active household.", response.Message);
        Assert.Equal("clerk_123", fakeService.LastClerkUserId);
    }

    [Fact]
    public async Task ClearAll_ReturnsServerError_WhenUnexpectedFailure()
    {
        var fakeService = new FakeChecklistService
        {
            ClearAllResultToReturn = ChecklistResult<int>.Fail(ChecklistError.InvalidRequest)
        };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));

        var result = await controller.ClearAll(CancellationToken.None);

        var serverError = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, serverError.StatusCode);
        var response = Assert.IsType<ApiResponse<int>>(serverError.Value);
        Assert.Equal(500, response.Code);
        Assert.Equal("Failed to clear items.", response.Message);
        Assert.Equal("clerk_123", fakeService.LastClerkUserId);
    }

    [Fact]
    public async Task ClearAll_ReturnsOk_WhenSucceeds()
    {
        var fakeService = new FakeChecklistService
        {
            ClearAllResultToReturn = ChecklistResult<int>.Ok(5)
        };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));

        var result = await controller.ClearAll(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<int>>(ok.Value);
        Assert.Equal(0, response.Code);
        Assert.Equal("Cleared 5 items.", response.Message);
        Assert.Equal(5, response.Data);
        Assert.Equal("clerk_123", fakeService.LastClerkUserId);
    }

    [Fact]
    public async Task ClearAll_ReturnsOk_WithZeroItems_WhenListEmpty()
    {
        var fakeService = new FakeChecklistService
        {
            ClearAllResultToReturn = ChecklistResult<int>.Ok(0)
        };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));

        var result = await controller.ClearAll(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<int>>(ok.Value);
        Assert.Equal(0, response.Code);
        Assert.Equal("Cleared 0 items.", response.Message);
        Assert.Equal(0, response.Data);
    }

    #endregion

    #region ClearChecked Tests

    [Fact]
    public async Task ClearChecked_ReturnsUnauthorized_WhenClerkUserIdMissing()
    {
        var fakeService = new FakeChecklistService();
        var controller = CreateController(fakeService, new ClaimsPrincipal(new ClaimsIdentity()));

        var result = await controller.ClearChecked(CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<int>>(unauthorized.Value);
        Assert.Equal(401, response.Code);
        Assert.Equal("Could not determine user.", response.Message);
    }

    [Fact]
    public async Task ClearChecked_ReturnsBadRequest_WhenNoActiveHousehold()
    {
        var fakeService = new FakeChecklistService
        {
            ClearCheckedResultToReturn = ChecklistResult<int>.Fail(ChecklistError.NoActiveHousehold)
        };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));

        var result = await controller.ClearChecked(CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<int>>(badRequest.Value);
        Assert.Equal(400, response.Code);
        Assert.Equal("No active household.", response.Message);
    }

    [Fact]
    public async Task ClearChecked_ReturnsOk_WhenSucceeds()
    {
        var fakeService = new FakeChecklistService
        {
            ClearCheckedResultToReturn = ChecklistResult<int>.Ok(3)
        };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));

        var result = await controller.ClearChecked(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<int>>(ok.Value);
        Assert.Equal(0, response.Code);
        Assert.Equal("Cleared 3 items.", response.Message);
        Assert.Equal(3, response.Data);
    }

    #endregion

    #region GetItems Tests

    [Fact]
    public async Task GetItems_ReturnsUnauthorized_WhenClerkUserIdMissing()
    {
        var fakeService = new FakeChecklistService();
        var controller = CreateController(fakeService, new ClaimsPrincipal(new ClaimsIdentity()));

        var result = await controller.GetItems(CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<ChecklistListDto>>(unauthorized.Value);
        Assert.Equal(401, response.Code);
    }

    [Fact]
    public async Task GetItems_ReturnsOk_WhenSucceeds()
    {
        var items = new List<ChecklistItemDto>
        {
            NewChecklistItemDto()
        };
        var fakeService = new FakeChecklistService
        {
            GetItemsResultToReturn = ChecklistResult<ChecklistListDto>.Ok(new ChecklistListDto(items, 1))
        };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));

        var result = await controller.GetItems(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<ChecklistListDto>>(ok.Value);
        Assert.Equal(0, response.Code);
        Assert.NotNull(response.Data);
        Assert.Single(response.Data.Items);
    }

    #endregion

    #region DeleteItem Tests

    [Fact]
    public async Task DeleteItem_ReturnsUnauthorized_WhenClerkUserIdMissing()
    {
        var fakeService = new FakeChecklistService();
        var controller = CreateController(fakeService, new ClaimsPrincipal(new ClaimsIdentity()));

        var result = await controller.DeleteItem(DefaultItemId, CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(unauthorized.Value);
        Assert.Equal(401, response.Code);
    }

    [Fact]
    public async Task DeleteItem_ReturnsNotFound_WhenItemMissing()
    {
        var fakeService = new FakeChecklistService
        {
            DeleteResultToReturn = ChecklistActionResult.Fail(ChecklistError.ItemNotFound)
        };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));

        var result = await controller.DeleteItem(DefaultItemId, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(notFound.Value);
        Assert.Equal(404, response.Code);
        Assert.Equal("Item not found.", response.Message);
    }

    [Fact]
    public async Task DeleteItem_ReturnsForbidden_WhenHouseholdMismatch()
    {
        var fakeService = new FakeChecklistService
        {
            DeleteResultToReturn = ChecklistActionResult.Fail(ChecklistError.HouseholdMismatch)
        };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));

        var result = await controller.DeleteItem(DefaultItemId, CancellationToken.None);

        var forbidden = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(403, forbidden.StatusCode);
        var response = Assert.IsType<ApiResponse>(forbidden.Value);
        Assert.Equal(403, response.Code);
        Assert.Equal("Access denied.", response.Message);
    }

    [Fact]
    public async Task DeleteItem_ReturnsOk_WhenSucceeds()
    {
        var fakeService = new FakeChecklistService
        {
            DeleteResultToReturn = ChecklistActionResult.Ok()
        };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));

        var result = await controller.DeleteItem(DefaultItemId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<object>>(ok.Value);
        Assert.Equal(0, response.Code);
        Assert.Equal("Ok", response.Message);
        Assert.Equal("Item deleted.", response.Data);
    }

    #endregion

    private static ChecklistController CreateController(IChecklistService service, ClaimsPrincipal user) =>
        new(service, NullLogger<ChecklistController>.Instance)
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

    private static ChecklistItemDto NewChecklistItemDto() =>
        new(
            DefaultItemId,
            "Tomatoes",
            2m,
            "pcs",
            "Vegetables",
            false,
            null,
            null,
            DefaultCreatedAt,
            DefaultCreatedAt
        );

    private sealed class FakeChecklistService : IChecklistService
    {
        public ChecklistResult<ChecklistListDto>? GetItemsResultToReturn { get; set; }
        public ChecklistResult<ChecklistItemDto>? AddItemResultToReturn { get; set; }
        public ChecklistResult<List<ChecklistItemDto>>? AddBatchResultToReturn { get; set; }
        public ChecklistResult<ChecklistItemDto>? UpdateItemResultToReturn { get; set; }
        public ChecklistActionResult? DeleteResultToReturn { get; set; }
        public ChecklistResult<int>? ClearCheckedResultToReturn { get; set; }
        public ChecklistResult<int>? ClearAllResultToReturn { get; set; }
        public ChecklistResult<ChecklistStatsDto>? GetStatsResultToReturn { get; set; }
        public ChecklistResult<MoveToInventoryResultDto>? MoveToInventoryResultToReturn { get; set; }

        public string? LastClerkUserId { get; private set; }
        public Guid? LastItemId { get; private set; }

        public Task<ChecklistResult<ChecklistListDto>> GetItemsAsync(
            string clerkUserId,
            CancellationToken cancellationToken = default)
        {
            LastClerkUserId = clerkUserId;
            return Task.FromResult(GetItemsResultToReturn ?? ChecklistResult<ChecklistListDto>.Ok(new ChecklistListDto([], 0)));
        }

        public Task<ChecklistResult<ChecklistItemDto>> AddItemAsync(
            string clerkUserId,
            CreateChecklistItemDto dto,
            CancellationToken cancellationToken = default)
        {
            LastClerkUserId = clerkUserId;
            return Task.FromResult(AddItemResultToReturn ?? ChecklistResult<ChecklistItemDto>.Ok(NewChecklistItemDto()));
        }

        public Task<ChecklistResult<List<ChecklistItemDto>>> AddBatchAsync(
            string clerkUserId,
            BatchCreateChecklistItemsDto dto,
            CancellationToken cancellationToken = default)
        {
            LastClerkUserId = clerkUserId;
            return Task.FromResult(AddBatchResultToReturn ?? ChecklistResult<List<ChecklistItemDto>>.Ok([]));
        }

        public Task<ChecklistResult<ChecklistItemDto>> UpdateItemAsync(
            Guid id,
            string clerkUserId,
            UpdateChecklistItemDto dto,
            CancellationToken cancellationToken = default)
        {
            LastItemId = id;
            LastClerkUserId = clerkUserId;
            return Task.FromResult(UpdateItemResultToReturn ?? ChecklistResult<ChecklistItemDto>.Ok(NewChecklistItemDto()));
        }

        public Task<ChecklistActionResult> DeleteItemAsync(
            Guid id,
            string clerkUserId,
            CancellationToken cancellationToken = default)
        {
            LastItemId = id;
            LastClerkUserId = clerkUserId;
            return Task.FromResult(DeleteResultToReturn ?? ChecklistActionResult.Ok());
        }

        public Task<ChecklistResult<int>> ClearCheckedAsync(
            string clerkUserId,
            CancellationToken cancellationToken = default)
        {
            LastClerkUserId = clerkUserId;
            return Task.FromResult(ClearCheckedResultToReturn ?? ChecklistResult<int>.Ok(0));
        }

        public Task<ChecklistResult<int>> ClearAllAsync(
            string clerkUserId,
            CancellationToken cancellationToken = default)
        {
            LastClerkUserId = clerkUserId;
            return Task.FromResult(ClearAllResultToReturn ?? ChecklistResult<int>.Ok(0));
        }

        public Task<ChecklistResult<ChecklistStatsDto>> GetStatsAsync(
            string clerkUserId,
            CancellationToken cancellationToken = default)
        {
            LastClerkUserId = clerkUserId;
            return Task.FromResult(GetStatsResultToReturn ?? ChecklistResult<ChecklistStatsDto>.Ok(new ChecklistStatsDto(0, 0, 0)));
        }

        public Task<ChecklistResult<MoveToInventoryResultDto>> MoveCheckedToInventoryAsync(
            string clerkUserId,
            CancellationToken cancellationToken = default)
        {
            LastClerkUserId = clerkUserId;
            return Task.FromResult(MoveToInventoryResultToReturn ?? ChecklistResult<MoveToInventoryResultDto>.Ok(new MoveToInventoryResultDto(0)));
        }
    }
}
