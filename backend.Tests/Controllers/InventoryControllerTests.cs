using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using backend.Controllers;
using backend.Dtos;
using backend.Dtos.Inventory;
using backend.Interfaces;
using backend.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace backend.Tests.Controllers;

public class InventoryControllerTests
{
    private static readonly Guid DefaultItemId = Guid.Parse("00000000-0000-0000-0000-000000000011");
    private static readonly Guid DefaultItemId2 = Guid.Parse("00000000-0000-0000-0000-000000000021");
    private static readonly Guid DefaultHouseholdId = Guid.Parse("00000000-0000-0000-0000-000000000012");
    private static readonly Guid DefaultIngredientId = Guid.Parse("00000000-0000-0000-0000-000000000013");
    private static readonly DateTime DefaultCreatedAt = new(2024, 01, 01, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime DefaultResolvedAt = new(2024, 01, 02, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CreateInventoryItemAsync_ReturnsUnauthorized_WhenClerkUserIdMissing()
    {
        var fakeService = new FakeInventoryService();
        var controller = CreateController(fakeService, new ClaimsPrincipal(new ClaimsIdentity()));
        var request = NewCreateRequest();

        var result = await controller.CreateInventoryItemAsync(request, CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(unauthorized.Value);
        Assert.Equal(401, response.Code);
        Assert.Equal("Could not determine Clerk user id from token.", response.Message);
        Assert.Null(response.Data);
        Assert.Null(fakeService.LastClerkUserId);
        Assert.Null(fakeService.LastCreateRequest);
    }

    [Fact]
    public async Task CreateInventoryItemAsync_ReturnsUnauthorized_WhenNoActiveHousehold()
    {
        var fakeService = new FakeInventoryService
        {
            CreateResultToReturn = InventoryResult<InventoryItemResponseDto>.Fail(InventoryError.NoActiveHousehold)
        };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));
        var request = NewCreateRequest();

        var result = await controller.CreateInventoryItemAsync(request, CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(unauthorized.Value);
        Assert.Equal(401, response.Code);
        Assert.Equal("No active household.", response.Message);
        Assert.Null(response.Data);
        Assert.Equal("clerk_123", fakeService.LastClerkUserId);
        Assert.Same(request, fakeService.LastCreateRequest);
    }

    [Fact]
    public async Task CreateBatchAsync_ReturnsUnauthorized_WhenClerkUserIdMissing()
    {
        var fakeService = new FakeInventoryService();
        var controller = CreateController(fakeService, new ClaimsPrincipal(new ClaimsIdentity()));
        var requests = NewBatchCreateRequests();

        var result = await controller.CreateBatchAsync(requests, CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(unauthorized.Value);
        Assert.Equal(401, response.Code);
        Assert.Equal("Could not determine Clerk user id from token.", response.Message);
        Assert.Null(response.Data);
        Assert.Null(fakeService.LastClerkUserId);
        Assert.Null(fakeService.LastCreateBatchRequests);
    }

    [Fact]
    public async Task CreateBatchAsync_ReturnsBadRequest_WhenNoItems()
    {
        var fakeService = new FakeInventoryService();
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));

        var result = await controller.CreateBatchAsync([], CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(badRequest.Value);
        Assert.Equal(400, response.Code);
        Assert.Equal("At least one inventory item is required.", response.Message);
        Assert.Null(response.Data);
        Assert.Null(fakeService.LastCreateBatchRequests);
    }

    [Fact]
    public async Task CreateBatchAsync_ReturnsUnauthorized_WhenNoActiveHousehold()
    {
        var fakeService = new FakeInventoryService
        {
            CreateBatchResultToReturn = InventoryResult<List<InventoryItemResponseDto>>.Fail(InventoryError.NoActiveHousehold)
        };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));
        var requests = NewBatchCreateRequests();

        var result = await controller.CreateBatchAsync(requests, CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(unauthorized.Value);
        Assert.Equal(401, response.Code);
        Assert.Equal("No active household.", response.Message);
        Assert.Null(response.Data);
        Assert.Equal("clerk_123", fakeService.LastClerkUserId);
        Assert.Same(requests, fakeService.LastCreateBatchRequests);
    }

    [Fact]
    public async Task CreateBatchAsync_ReturnsOk_WhenCreateSucceeds()
    {
        var createdItems = NewBatchResponseDtos();
        var fakeService = new FakeInventoryService
        {
            CreateBatchResultToReturn = InventoryResult<List<InventoryItemResponseDto>>.Ok(createdItems)
        };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));
        var requests = NewBatchCreateRequests();

        var result = await controller.CreateBatchAsync(requests, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<List<InventoryItemResponseDto>>>(ok.Value);
        Assert.Equal(0, response.Code);
        Assert.Equal("Ok", response.Message);
        var data = Assert.IsType<List<InventoryItemResponseDto>>(response.Data);
        Assert.Equal(2, data.Count);
        Assert.Equal(DefaultItemId, data[0].Id);
        Assert.Equal(DefaultItemId2, data[1].Id);
        Assert.Equal("clerk_123", fakeService.LastClerkUserId);
        Assert.Same(requests, fakeService.LastCreateBatchRequests);
    }

    [Fact]
    public async Task CreateInventoryItemAsync_ReturnsServerError_WhenUnexpectedFailure()
    {
        var fakeService = new FakeInventoryService
        {
            CreateResultToReturn = InventoryResult<InventoryItemResponseDto>.Fail(InventoryError.IngredientNotFound)
        };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));
        var request = NewCreateRequest();

        var result = await controller.CreateInventoryItemAsync(request, CancellationToken.None);

        var serverError = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, serverError.StatusCode);
        var response = Assert.IsType<ApiResponse>(serverError.Value);
        Assert.Equal(500, response.Code);
        Assert.Equal("An unexpected error occurred.", response.Message);
        Assert.Null(response.Data);
        Assert.Equal("clerk_123", fakeService.LastClerkUserId);
        Assert.Same(request, fakeService.LastCreateRequest);
    }

    [Fact]
    public async Task CreateInventoryItemAsync_ReturnsOk_WhenCreateSucceeds()
    {
        var createdItem = NewResponseDto();
        var fakeService = new FakeInventoryService
        {
            CreateResultToReturn = InventoryResult<InventoryItemResponseDto>.Ok(createdItem)
        };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));
        var request = NewCreateRequest();

        var result = await controller.CreateInventoryItemAsync(request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<InventoryItemResponseDto>>(ok.Value);
        Assert.Equal(0, response.Code);
        Assert.Equal("Ok", response.Message);
        var data = Assert.IsType<InventoryItemResponseDto>(response.Data);
        Assert.Equal(DefaultItemId, data.Id);
        Assert.Equal(DefaultHouseholdId, data.HouseholdId);
        Assert.Equal(DefaultIngredientId, data.IngredientId);
        Assert.Equal("Tomatoes", data.Name);
        Assert.Equal(2m, data.Amount);
        Assert.Equal("pcs", data.Unit);
        Assert.Equal(InventoryStorageMethod.RoomTemp, data.StorageMethod);
        Assert.Equal(5, data.DaysRemaining);
        Assert.Equal(DefaultCreatedAt, data.CreatedAt);
        Assert.Equal("clerk_123", fakeService.LastClerkUserId);
        Assert.Same(request, fakeService.LastCreateRequest);
    }

    [Fact]
    public async Task GetInventoryAsync_ReturnsUnauthorized_WhenClerkUserIdMissing()
    {
        var fakeService = new FakeInventoryService();
        var controller = CreateController(fakeService, new ClaimsPrincipal(new ClaimsIdentity()));
        var query = NewListRequest();

        var result = await controller.GetInventoryAsync(query, CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(unauthorized.Value);
        Assert.Equal(401, response.Code);
        Assert.Equal("Could not determine Clerk user id from token.", response.Message);
        Assert.Null(response.Data);
        Assert.Null(fakeService.LastClerkUserId);
        Assert.Null(fakeService.LastListQuery);
    }

    [Fact]
    public async Task GetInventoryAsync_ReturnsUnauthorized_WhenNoActiveHousehold()
    {
        var fakeService = new FakeInventoryService
        {
            GetInventoryResultToReturn = InventoryResult<InventoryListResponseDto>.Fail(InventoryError.NoActiveHousehold)
        };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));
        var query = NewListRequest();

        var result = await controller.GetInventoryAsync(query, CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(unauthorized.Value);
        Assert.Equal(401, response.Code);
        Assert.Equal("No active household.", response.Message);
        Assert.Null(response.Data);
        Assert.Equal("clerk_123", fakeService.LastClerkUserId);
        Assert.Same(query, fakeService.LastListQuery);
    }

    [Fact]
    public async Task GetInventoryAsync_ReturnsServerError_WhenUnexpectedFailure()
    {
        var fakeService = new FakeInventoryService
        {
            GetInventoryResultToReturn = InventoryResult<InventoryListResponseDto>.Fail(InventoryError.IngredientNotFound)
        };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));
        var query = NewListRequest();

        var result = await controller.GetInventoryAsync(query, CancellationToken.None);

        var serverError = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, serverError.StatusCode);
        var response = Assert.IsType<ApiResponse>(serverError.Value);
        Assert.Equal(500, response.Code);
        Assert.Equal("Unexpected error", response.Message);
        Assert.Null(response.Data);
        Assert.Equal("clerk_123", fakeService.LastClerkUserId);
        Assert.Same(query, fakeService.LastListQuery);
    }

    [Fact]
    public async Task GetInventoryAsync_ReturnsOk_WhenQuerySucceeds()
    {
        var listResponse = NewListResponseDto();
        var fakeService = new FakeInventoryService
        {
            GetInventoryResultToReturn = InventoryResult<InventoryListResponseDto>.Ok(listResponse)
        };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));
        var query = NewListRequest();

        var result = await controller.GetInventoryAsync(query, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<InventoryListResponseDto>>(ok.Value);
        Assert.Equal(0, response.Code);
        Assert.Equal("Ok", response.Message);
        var data = Assert.IsType<InventoryListResponseDto>(response.Data);
        Assert.Equal(listResponse.Data.Count, data.Data.Count);
        Assert.Equal(listResponse.TotalCount, data.TotalCount);
        Assert.Equal(listResponse.Page, data.Page);
        Assert.Equal(listResponse.PageSize, data.PageSize);
        Assert.Equal(listResponse.TotalPages, data.TotalPages);
        Assert.Equal("clerk_123", fakeService.LastClerkUserId);
        Assert.Same(query, fakeService.LastListQuery);
    }

    [Fact]
    public async Task GetInventoryStatsAsync_ReturnsUnauthorized_WhenClerkUserIdMissing()
    {
        var fakeService = new FakeInventoryService();
        var controller = CreateController(fakeService, new ClaimsPrincipal(new ClaimsIdentity()));

        var result = await controller.GetInventoryStatsAsync(CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(unauthorized.Value);
        Assert.Equal(401, response.Code);
        Assert.Equal("Could not determine Clerk user id from token.", response.Message);
        Assert.Null(response.Data);
        Assert.Null(fakeService.LastClerkUserId);
    }

    [Fact]
    public async Task GetInventoryStatsAsync_ReturnsUnauthorized_WhenNoActiveHousehold()
    {
        var fakeService = new FakeInventoryService
        {
            GetStatsResultToReturn = InventoryResult<InventoryStatsResponseDto>.Fail(InventoryError.NoActiveHousehold)
        };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));

        var result = await controller.GetInventoryStatsAsync(CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(unauthorized.Value);
        Assert.Equal(401, response.Code);
        Assert.Equal("No active household.", response.Message);
        Assert.Null(response.Data);
        Assert.Equal("clerk_123", fakeService.LastClerkUserId);
    }

    [Fact]
    public async Task GetInventoryStatsAsync_ReturnsOk_WhenSucceeded()
    {
        var stats = NewStatsResponseDto();
        var fakeService = new FakeInventoryService
        {
            GetStatsResultToReturn = InventoryResult<InventoryStatsResponseDto>.Ok(stats)
        };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));

        var result = await controller.GetInventoryStatsAsync(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<InventoryStatsResponseDto>>(ok.Value);
        Assert.Equal(0, response.Code);
        Assert.Equal("Ok", response.Message);
        var data = Assert.IsType<InventoryStatsResponseDto>(response.Data);
        Assert.Equal(10, data.TotalCount);
        Assert.Equal(2, data.ExpiringSoonCount);
        Assert.Equal(2, data.StorageMethodCount);
        Assert.Equal("clerk_123", fakeService.LastClerkUserId);
    }

    [Fact]
    public async Task UpdateInventoryItemAsync_ReturnsUnauthorized_WhenClerkUserIdMissing()
    {
        var fakeService = new FakeInventoryService();
        var controller = CreateController(fakeService, new ClaimsPrincipal(new ClaimsIdentity()));
        var request = NewUpdateRequest();

        var result = await controller.UpdateInventoryItemAsync(DefaultItemId, request, CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(unauthorized.Value);
        Assert.Equal(401, response.Code);
        Assert.Equal("Could not determine Clerk user id from token.", response.Message);
        Assert.Null(response.Data);
        Assert.Null(fakeService.LastClerkUserId);
        Assert.Null(fakeService.LastUpdateRequest);
    }

    [Fact]
    public async Task UpdateInventoryItemAsync_ReturnsUnauthorized_WhenNoActiveHousehold()
    {
        var fakeService = new FakeInventoryService
        {
            UpdateResultToReturn = InventoryResult<InventoryItemResponseDto>.Fail(InventoryError.NoActiveHousehold)
        };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));
        var request = NewUpdateRequest();

        var result = await controller.UpdateInventoryItemAsync(DefaultItemId, request, CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(unauthorized.Value);
        Assert.Equal(401, response.Code);
        Assert.Equal("No active household.", response.Message);
        Assert.Null(response.Data);
        Assert.Equal("clerk_123", fakeService.LastClerkUserId);
        Assert.Equal(DefaultItemId, fakeService.LastItemId);
        Assert.Same(request, fakeService.LastUpdateRequest);
    }

    [Fact]
    public async Task UpdateInventoryItemAsync_ReturnsNotFound_WhenItemMissing()
    {
        var fakeService = new FakeInventoryService
        {
            UpdateResultToReturn = InventoryResult<InventoryItemResponseDto>.Fail(InventoryError.ItemNotFound)
        };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));
        var request = NewUpdateRequest();

        var result = await controller.UpdateInventoryItemAsync(DefaultItemId, request, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(notFound.Value);
        Assert.Equal(404, response.Code);
        Assert.Equal("Inventory item not found.", response.Message);
        Assert.Null(response.Data);
        Assert.Equal("clerk_123", fakeService.LastClerkUserId);
        Assert.Equal(DefaultItemId, fakeService.LastItemId);
        Assert.Same(request, fakeService.LastUpdateRequest);
    }

    [Fact]
    public async Task UpdateInventoryItemAsync_ReturnsForbidden_WhenHouseholdMismatch()
    {
        var fakeService = new FakeInventoryService
        {
            UpdateResultToReturn = InventoryResult<InventoryItemResponseDto>.Fail(InventoryError.HouseholdMismatch)
        };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));
        var request = NewUpdateRequest();

        var result = await controller.UpdateInventoryItemAsync(DefaultItemId, request, CancellationToken.None);

        var forbidden = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(403, forbidden.StatusCode);
        var response = Assert.IsType<ApiResponse>(forbidden.Value);
        Assert.Equal(403, response.Code);
        Assert.Equal("You do not have the permission to update this item.", response.Message);
        Assert.Null(response.Data);
        Assert.Equal("clerk_123", fakeService.LastClerkUserId);
        Assert.Equal(DefaultItemId, fakeService.LastItemId);
        Assert.Same(request, fakeService.LastUpdateRequest);
    }

    [Fact]
    public async Task UpdateInventoryItemAsync_ReturnsServerError_WhenUnexpectedFailure()
    {
        var fakeService = new FakeInventoryService
        {
            UpdateResultToReturn = InventoryResult<InventoryItemResponseDto>.Fail(InventoryError.IngredientNotFound)
        };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));
        var request = NewUpdateRequest();

        var result = await controller.UpdateInventoryItemAsync(DefaultItemId, request, CancellationToken.None);

        var serverError = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, serverError.StatusCode);
        var response = Assert.IsType<ApiResponse>(serverError.Value);
        Assert.Equal(500, response.Code);
        Assert.Equal("Unexpected inventory error.", response.Message);
        Assert.Null(response.Data);
        Assert.Equal("clerk_123", fakeService.LastClerkUserId);
        Assert.Equal(DefaultItemId, fakeService.LastItemId);
        Assert.Same(request, fakeService.LastUpdateRequest);
    }

    [Fact]
    public async Task UpdateInventoryItemAsync_ReturnsOk_WhenUpdateSucceeds()
    {
        var updatedItem = NewResponseDto();
        var fakeService = new FakeInventoryService
        {
            UpdateResultToReturn = InventoryResult<InventoryItemResponseDto>.Ok(updatedItem)
        };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));
        var request = NewUpdateRequest();

        var result = await controller.UpdateInventoryItemAsync(DefaultItemId, request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<InventoryItemResponseDto>>(ok.Value);
        Assert.Equal(0, response.Code);
        Assert.Equal("Ok", response.Message);
        var data = Assert.IsType<InventoryItemResponseDto>(response.Data);
        Assert.Equal(DefaultItemId, data.Id);
        Assert.Equal(DefaultHouseholdId, data.HouseholdId);
        Assert.Equal(DefaultIngredientId, data.IngredientId);
        Assert.Equal("Tomatoes", data.Name);
        Assert.Equal(2m, data.Amount);
        Assert.Equal("pcs", data.Unit);
        Assert.Equal(InventoryStorageMethod.RoomTemp, data.StorageMethod);
        Assert.Equal(5, data.DaysRemaining);
        Assert.Equal(DefaultCreatedAt, data.CreatedAt);
        Assert.Equal("clerk_123", fakeService.LastClerkUserId);
        Assert.Equal(DefaultItemId, fakeService.LastItemId);
        Assert.Same(request, fakeService.LastUpdateRequest);
    }

    [Fact]
    public async Task DeleteInventoryItemAsync_ReturnsUnauthorized_WhenClerkUserIdMissing()
    {
        var fakeService = new FakeInventoryService();
        var controller = CreateController(fakeService, new ClaimsPrincipal(new ClaimsIdentity()));

        var result = await controller.DeleteInventoryItemAsync(DefaultItemId, CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(unauthorized.Value);
        Assert.Equal(401, response.Code);
        Assert.Equal("Could not determine Clerk user id from token.", response.Message);
        Assert.Null(response.Data);
        Assert.Null(fakeService.LastClerkUserId);
        Assert.Null(fakeService.LastItemId);
    }

    [Fact]
    public async Task DeleteInventoryItemAsync_ReturnsUnauthorized_WhenNoActiveHousehold()
    {
        var fakeService = new FakeInventoryService
        {
            DeleteResultToReturn = InventoryActionResult.Fail(InventoryError.NoActiveHousehold)
        };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));

        var result = await controller.DeleteInventoryItemAsync(DefaultItemId, CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(unauthorized.Value);
        Assert.Equal(401, response.Code);
        Assert.Equal("No active household.", response.Message);
        Assert.Null(response.Data);
        Assert.Equal("clerk_123", fakeService.LastClerkUserId);
        Assert.Equal(DefaultItemId, fakeService.LastItemId);
    }

    [Fact]
    public async Task DeleteInventoryItemAsync_ReturnsNotFound_WhenItemMissing()
    {
        var fakeService = new FakeInventoryService
        {
            DeleteResultToReturn = InventoryActionResult.Fail(InventoryError.ItemNotFound)
        };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));

        var result = await controller.DeleteInventoryItemAsync(DefaultItemId, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(notFound.Value);
        Assert.Equal(404, response.Code);
        Assert.Equal("Inventory item not found.", response.Message);
        Assert.Null(response.Data);
        Assert.Equal("clerk_123", fakeService.LastClerkUserId);
        Assert.Equal(DefaultItemId, fakeService.LastItemId);
    }

    [Fact]
    public async Task DeleteInventoryItemAsync_ReturnsForbidden_WhenHouseholdMismatch()
    {
        var fakeService = new FakeInventoryService
        {
            DeleteResultToReturn = InventoryActionResult.Fail(InventoryError.HouseholdMismatch)
        };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));

        var result = await controller.DeleteInventoryItemAsync(DefaultItemId, CancellationToken.None);

        var forbidden = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(403, forbidden.StatusCode);
        var response = Assert.IsType<ApiResponse>(forbidden.Value);
        Assert.Equal(403, response.Code);
        Assert.Equal("You do not have permission to delete this item.", response.Message);
        Assert.Null(response.Data);
        Assert.Equal("clerk_123", fakeService.LastClerkUserId);
        Assert.Equal(DefaultItemId, fakeService.LastItemId);
    }

    [Fact]
    public async Task DeleteInventoryItemAsync_ReturnsServerError_WhenUnexpectedFailure()
    {
        var fakeService = new FakeInventoryService
        {
            DeleteResultToReturn = InventoryActionResult.Fail(InventoryError.IngredientNotFound)
        };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));

        var result = await controller.DeleteInventoryItemAsync(DefaultItemId, CancellationToken.None);

        var serverError = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, serverError.StatusCode);
        var response = Assert.IsType<ApiResponse>(serverError.Value);
        Assert.Equal(500, response.Code);
        Assert.Equal("Unexpected inventory error.", response.Message);
        Assert.Null(response.Data);
        Assert.Equal("clerk_123", fakeService.LastClerkUserId);
        Assert.Equal(DefaultItemId, fakeService.LastItemId);
    }

    [Fact]
    public async Task DeleteInventoryItemAsync_ReturnsOk_WhenDeleteSucceeds()
    {
        var fakeService = new FakeInventoryService
        {
            DeleteResultToReturn = InventoryActionResult.Ok()
        };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));

        var result = await controller.DeleteInventoryItemAsync(DefaultItemId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<object>>(ok.Value);
        Assert.Equal(0, response.Code);
        Assert.Equal("Ok", response.Message);
        Assert.Equal("Inventory item deleted.", response.Data);
        Assert.Equal("clerk_123", fakeService.LastClerkUserId);
        Assert.Equal(DefaultItemId, fakeService.LastItemId);
    }

    private static InventoryController CreateController(IInventoryService service, ClaimsPrincipal user) =>
        new(service, NullLogger<InventoryController>.Instance)
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

    private static CreateInventoryItemRequestDto NewCreateRequest() => new()
    {
        Name = "Tomatoes",
        Amount = 2,
        Unit = "pcs",
        StorageMethod = InventoryStorageMethod.RoomTemp,
        ExpirationDays = 3
    };

    private static List<CreateInventoryItemRequestDto> NewBatchCreateRequests() =>
        [NewCreateRequest(), NewCreateRequest()];

    private static UpdateInventoryItemRequestDto NewUpdateRequest() => new()
    {
        Amount = 2,
        Unit = "pcs",
        StorageMethod = InventoryStorageMethod.RoomTemp,
        ExpirationDays = 3
    };

    private static InventoryItemResponseDto NewResponseDto() => NewResponseDto(DefaultItemId);

    private static InventoryItemResponseDto NewResponseDto(Guid id) =>
        new(
            id,
            DefaultHouseholdId,
            DefaultIngredientId,
            "Tomatoes",
            "tomatoes",
            IngredientResolveStatus.Resolved,
            0.87m,
            "auto",
            DefaultResolvedAt,
            1,
            null,
            2m,
            "pcs",
            InventoryStorageMethod.RoomTemp,
            5,
            DefaultCreatedAt
        );

    private static InventoryListRequestDto NewListRequest() => new()
    {
        Keyword = "tomato",
        StorageMethod = InventoryStorageMethod.RoomTemp,
        SortBy = InventorySortBy.Name,
        SortOrder = SortOrder.Asc,
        Page = 2,
        PageSize = 10
    };

    private static InventoryStatsResponseDto NewStatsResponseDto() =>
        new(
            10,
            2,
            2
        );

    private static InventoryListResponseDto NewListResponseDto() =>
        new(
            [NewResponseDto(DefaultItemId), NewResponseDto(DefaultItemId2)],
            2,
            1,
            10
        );

    private static List<InventoryItemResponseDto> NewBatchResponseDtos() =>
        [NewResponseDto(DefaultItemId), NewResponseDto(DefaultItemId2)];

    private sealed class FakeInventoryService : IInventoryService
    {
        public InventoryResult<InventoryItemResponseDto>? CreateResultToReturn { get; set; }
        public InventoryResult<List<InventoryItemResponseDto>>? CreateBatchResultToReturn { get; set; }
        public InventoryResult<InventoryListResponseDto>? GetInventoryResultToReturn { get; set; }
        public InventoryResult<InventoryStatsResponseDto>? GetStatsResultToReturn { get; set; }
        public InventoryResult<InventoryItemResponseDto>? UpdateResultToReturn { get; set; }
        public InventoryActionResult? DeleteResultToReturn { get; set; }
        public Guid? LastItemId { get; private set; }
        public string? LastClerkUserId { get; private set; }
        public UpdateInventoryItemRequestDto? LastUpdateRequest { get; private set; }
        public CreateInventoryItemRequestDto? LastCreateRequest { get; private set; }
        public IReadOnlyList<CreateInventoryItemRequestDto>? LastCreateBatchRequests { get; private set; }
        public InventoryListRequestDto? LastListQuery { get; private set; }

        public Task<InventoryResult<InventoryItemResponseDto>> CreateInventoryItemAsync(
            string clerkUserId,
            CreateInventoryItemRequestDto request,
            CancellationToken cancellationToken = default)
        {
            LastClerkUserId = clerkUserId;
            LastCreateRequest = request;
            return Task.FromResult(CreateResultToReturn ?? InventoryResult<InventoryItemResponseDto>.Ok(NewResponseDto()));
        }

        public Task<InventoryResult<List<InventoryItemResponseDto>>> CreateBatchAsync(
            string clerkUserId,
            IReadOnlyList<CreateInventoryItemRequestDto> items,
            CancellationToken cancellationToken = default)
        {
            LastClerkUserId = clerkUserId;
            LastCreateBatchRequests = items;
            return Task.FromResult(CreateBatchResultToReturn ?? InventoryResult<List<InventoryItemResponseDto>>.Ok(NewBatchResponseDtos()));
        }

        public Task<InventoryResult<InventoryListResponseDto>> GetInventoryListAsync(
            string clerkUserId,
            InventoryListRequestDto query,
            CancellationToken cancellationToken = default)
        {
            LastClerkUserId = clerkUserId;
            LastListQuery = query;
            return Task.FromResult(GetInventoryResultToReturn ?? InventoryResult<InventoryListResponseDto>.Ok(NewListResponseDto()));
        }

        public Task<InventoryResult<InventoryStatsResponseDto>> GetInventoryStatsAsync(
            string clerkUserId,
            CancellationToken cancellationToken = default)
        {
            LastClerkUserId = clerkUserId;
            return Task.FromResult(GetStatsResultToReturn ?? InventoryResult<InventoryStatsResponseDto>.Ok(NewStatsResponseDto()));
        }

        public Task<InventoryResult<InventoryItemResponseDto>> UpdateInventoryItemAsync(
            Guid itemId,
            string clerkUserId,
            UpdateInventoryItemRequestDto request,
            CancellationToken cancellationToken = default)
        {
            LastItemId = itemId;
            LastClerkUserId = clerkUserId;
            LastUpdateRequest = request;
            return Task.FromResult(UpdateResultToReturn ?? InventoryResult<InventoryItemResponseDto>.Ok(NewResponseDto()));
        }

        public Task<InventoryActionResult> DeleteInventoryItemAsync(
            Guid itemId,
            string clerkUserId,
            CancellationToken cancellationToken = default)
        {
            LastItemId = itemId;
            LastClerkUserId = clerkUserId;
            return Task.FromResult(DeleteResultToReturn ?? InventoryActionResult.Ok());
        }

        public Task<InventoryResult<DeductionResult>> DeductForRecipeAsync(
            string clerkUserId,
            Guid recipeId,
            int servings,
            CancellationToken cancellationToken = default)
        {
            LastClerkUserId = clerkUserId;
            return Task.FromResult(InventoryResult<DeductionResult>.Ok(new DeductionResult(0, 0, 0, 0, [])));
        }
    }
}
