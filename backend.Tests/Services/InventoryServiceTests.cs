using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using backend.Data;
using backend.Dtos.Households;
using backend.Dtos.Inventory;
using backend.Interfaces;
using backend.Models;
using backend.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace backend.Tests.Services;

public class InventoryServiceTests
{
    private static readonly Guid HouseholdId = Guid.Parse("00000000-0000-0000-0000-000000000111");
    private static readonly Guid IngredientId = Guid.Parse("00000000-0000-0000-0000-000000000222");
    private static readonly Guid InventoryItemId = Guid.Parse("00000000-0000-0000-0000-000000000777");

    [Fact]
    public async Task CreateInventoryItemAsync_ReturnsNoActiveHousehold_WhenNoneExists()
    {
        var householdService = new FakeHouseholdService { ActiveHouseholdId = null };
        var repo = new FakeInventoryRepository();
        var service = CreateService(householdService, repo);
        var request = NewCreateRequest(expirationDays: 3);

        var result = await service.CreateInventoryItemAsync("clerk_123", request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(InventoryError.NoActiveHousehold, result.Result);
        Assert.Null(repo.LastAdded);
    }

    [Fact]
    public async Task CreateInventoryItemAsync_ReturnsSuccess_AndPersistsItem()
    {
        var householdService = new FakeHouseholdService { ActiveHouseholdId = HouseholdId };
        var repo = new FakeInventoryRepository();
        var service = CreateService(householdService, repo);
        var request = NewCreateRequest(expirationDays: 3);

        var result = await service.CreateInventoryItemAsync("clerk_123", request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var dto = Assert.IsType<InventoryItemResponseDto>(result.Data);
        Assert.NotEqual(Guid.Empty, dto.Id);
        Assert.Equal(HouseholdId, dto.HouseholdId);
        Assert.Null(dto.IngredientId);
        Assert.Equal(request.Name, dto.Name);
        Assert.Equal(request.Amount, dto.Amount);
        Assert.Equal(request.Unit, dto.Unit);
        Assert.Equal(request.StorageMethod, dto.StorageMethod);

        Assert.NotNull(repo.LastAdded);
        var persisted = repo.LastAdded!;
        Assert.Equal(dto.Id, persisted.Id);
        Assert.Equal(HouseholdId, persisted.HouseholdId);
        Assert.Null(persisted.IngredientId);
        Assert.Equal(request.Name, persisted.Name);
        Assert.Equal(request.Amount, persisted.Amount);
        Assert.Equal(request.Unit, persisted.Unit);
        Assert.Equal(request.StorageMethod, persisted.StorageMethod);
        Assert.True(persisted.ExpirationDate.HasValue);
    }

    [Fact]
    public async Task CreateBatchAsync_ReturnsNoActiveHousehold_WhenNoneExists()
    {
        var householdService = new FakeHouseholdService { ActiveHouseholdId = null };
        var repo = new FakeInventoryRepository();
        var service = CreateService(householdService, repo);
        var requests = NewBatchRequests();

        var result = await service.CreateBatchAsync("clerk_123", requests, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(InventoryError.NoActiveHousehold, result.Result);
        Assert.Empty(repo.LastAddedBatch);
    }

    [Fact]
    public async Task CreateBatchAsync_ReturnsSuccess_AndPersistsItems()
    {
        var householdService = new FakeHouseholdService { ActiveHouseholdId = HouseholdId };
        var repo = new FakeInventoryRepository();
        var service = CreateService(householdService, repo);
        var requests = NewBatchRequests();

        var result = await service.CreateBatchAsync("clerk_123", requests, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var data = Assert.IsType<List<InventoryItemResponseDto>>(result.Data);
        Assert.Equal(2, data.Count);
        Assert.All(data, dto =>
        {
            Assert.NotEqual(Guid.Empty, dto.Id);
            Assert.Equal(HouseholdId, dto.HouseholdId);
        });
        Assert.Equal(data.Select(d => d.Id).Count(), data.Select(d => d.Id).Distinct().Count());

        Assert.NotNull(repo.LastAddedBatch);
        Assert.Equal(2, repo.LastAddedBatch.Count);
        Assert.All(repo.LastAddedBatch, item =>
        {
            Assert.Equal(HouseholdId, item.HouseholdId);
            Assert.True(item.ExpirationDate.HasValue);
        });
    }

    [Fact]
    public async Task UpdateInventoryItemAsync_ReturnsItemNotFound_WhenMissing()
    {
        var householdService = new FakeHouseholdService { ActiveHouseholdId = HouseholdId };
        var repo = new FakeInventoryRepository { Item = null };
        var service = CreateService(householdService, repo);
        var request = NewUpdateRequest(expirationDays: 2);

        var result = await service.UpdateInventoryItemAsync(InventoryItemId, "clerk_123", request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(InventoryError.ItemNotFound, result.Result);
        Assert.False(repo.UpdateCalled);
    }

    [Fact]
    public async Task UpdateInventoryItemAsync_ReturnsNoActiveHousehold_WhenNone()
    {
        var householdService = new FakeHouseholdService { ActiveHouseholdId = null };
        var repo = new FakeInventoryRepository { Item = NewInventoryItem() };
        var service = CreateService(householdService, repo);
        var request = NewUpdateRequest(expirationDays: 2);

        var result = await service.UpdateInventoryItemAsync(InventoryItemId, "clerk_123", request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(InventoryError.NoActiveHousehold, result.Result);
        Assert.False(repo.UpdateCalled);
    }

    [Fact]
    public async Task UpdateInventoryItemAsync_ReturnsHouseholdMismatch_WhenDifferentHousehold()
    {
        var householdService = new FakeHouseholdService { ActiveHouseholdId = Guid.Parse("00000000-0000-0000-0000-000000000001") };
        var repo = new FakeInventoryRepository { Item = NewInventoryItem() };
        var service = CreateService(householdService, repo);
        var request = NewUpdateRequest(expirationDays: 2);

        var result = await service.UpdateInventoryItemAsync(InventoryItemId, "clerk_123", request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(InventoryError.HouseholdMismatch, result.Result);
        Assert.False(repo.UpdateCalled);
    }

    [Fact]
    public async Task UpdateInventoryItemAsync_UpdatesItemAndReturnsDto_WhenAuthorized()
    {
        var householdService = new FakeHouseholdService { ActiveHouseholdId = HouseholdId };
        var repo = new FakeInventoryRepository { Item = NewInventoryItem() };
        var service = CreateService(householdService, repo);
        var request = NewUpdateRequest(expirationDays: 7, amount: 5, unit: "kg", storage: InventoryStorageMethod.Refrigerated);

        var result = await service.UpdateInventoryItemAsync(InventoryItemId, "clerk_123", request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var dto = Assert.IsType<InventoryItemResponseDto>(result.Data);
        Assert.Equal(InventoryItemId, dto.Id);
        Assert.Equal(HouseholdId, dto.HouseholdId);
        Assert.Equal(IngredientId, dto.IngredientId);
        Assert.Equal(request.Amount, dto.Amount);
        Assert.Equal(request.Unit, dto.Unit);
        Assert.Equal(request.StorageMethod, dto.StorageMethod);

        Assert.True(repo.UpdateCalled);
        Assert.NotNull(repo.LastUpdated);
        var updated = repo.LastUpdated!;
        Assert.Equal(request.Amount, updated.Amount);
        Assert.Equal(request.Unit, updated.Unit);
        Assert.Equal(request.StorageMethod, updated.StorageMethod);
        Assert.True(updated.ExpirationDate.HasValue);
    }

    [Fact]
    public async Task DeleteInventoryItemAsync_ReturnsItemNotFound_WhenMissing()
    {
        var householdService = new FakeHouseholdService { ActiveHouseholdId = HouseholdId };
        var repo = new FakeInventoryRepository { Item = null };
        var service = CreateService(householdService, repo);

        var result = await service.DeleteInventoryItemAsync(InventoryItemId, "clerk_123", CancellationToken.None);

        Assert.Equal(InventoryError.ItemNotFound, result.Result);
        Assert.False(repo.DeleteCalled);
    }

    [Fact]
    public async Task DeleteInventoryItemAsync_ReturnsNoActiveHousehold_WhenNone()
    {
        var householdService = new FakeHouseholdService { ActiveHouseholdId = null };
        var repo = new FakeInventoryRepository { Item = NewInventoryItem() };
        var service = CreateService(householdService, repo);

        var result = await service.DeleteInventoryItemAsync(InventoryItemId, "clerk_123", CancellationToken.None);

        Assert.Equal(InventoryError.NoActiveHousehold, result.Result);
        Assert.False(repo.DeleteCalled);
    }

    [Fact]
    public async Task DeleteInventoryItemAsync_ReturnsHouseholdMismatch_WhenDifferentHousehold()
    {
        var householdService = new FakeHouseholdService { ActiveHouseholdId = Guid.Parse("00000000-0000-0000-0000-000000000001") };
        var repo = new FakeInventoryRepository { Item = NewInventoryItem() };
        var service = CreateService(householdService, repo);

        var result = await service.DeleteInventoryItemAsync(InventoryItemId, "clerk_123", CancellationToken.None);

        Assert.Equal(InventoryError.HouseholdMismatch, result.Result);
        Assert.False(repo.DeleteCalled);
    }

    [Fact]
    public async Task DeleteInventoryItemAsync_ReturnsItemNotFound_WhenDeleteFails()
    {
        var householdService = new FakeHouseholdService { ActiveHouseholdId = HouseholdId };
        var repo = new FakeInventoryRepository { Item = NewInventoryItem(), DeleteResult = false };
        var service = CreateService(householdService, repo);

        var result = await service.DeleteInventoryItemAsync(InventoryItemId, "clerk_123", CancellationToken.None);

        Assert.Equal(InventoryError.ItemNotFound, result.Result);
        Assert.True(repo.DeleteCalled);
        Assert.Equal(InventoryItemId, repo.LastDeleteId);
    }

    [Fact]
    public async Task DeleteInventoryItemAsync_ReturnsOk_WhenDeleted()
    {
        var householdService = new FakeHouseholdService { ActiveHouseholdId = HouseholdId };
        var repo = new FakeInventoryRepository { Item = NewInventoryItem(), DeleteResult = true };
        var service = CreateService(householdService, repo);

        var result = await service.DeleteInventoryItemAsync(InventoryItemId, "clerk_123", CancellationToken.None);

        Assert.Equal(InventoryError.None, result.Result);
        Assert.True(repo.DeleteCalled);
        Assert.Equal(InventoryItemId, repo.LastDeleteId);
    }

    [Fact]
    public async Task GetInventoryListAsync_ReturnsNoActiveHousehold_WhenNone()
    {
        var householdService = new FakeHouseholdService { ActiveHouseholdId = null };
        var repo = new FakeInventoryRepository();
        var service = CreateService(householdService, repo);
        var query = NewListRequest();

        var result = await service.GetInventoryListAsync("clerk_123", query, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(InventoryError.NoActiveHousehold, result.Result);
        Assert.False(repo.QueryCalled);
    }

    [Fact]
    public async Task GetInventoryListAsync_ReturnsPagedItems_WhenAuthorized()
    {
        var householdService = new FakeHouseholdService { ActiveHouseholdId = HouseholdId };
        var repo = new FakeInventoryRepository();
        repo.QueryItems = [NewInventoryItem(), NewInventoryItem(Guid.Parse("00000000-0000-0000-0000-000000000778"), "Onions")];
        repo.QueryTotalCount = 5;
        var service = CreateService(householdService, repo);
        var query = NewListRequest(page: 0, pageSize: 500); // exercises clamping

        var result = await service.GetInventoryListAsync("clerk_123", query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var data = Assert.IsType<InventoryListResponseDto>(result.Data);
        Assert.Equal(2, data.Data.Count);
        Assert.Equal(5, data.TotalCount);
        Assert.Equal(1, data.Page); // clamped to minimum 1
        Assert.Equal(100, data.PageSize); // clamped to maximum 100
        Assert.Equal(1, data.TotalPages);

        Assert.True(repo.QueryCalled);
        Assert.Equal(HouseholdId, repo.LastQueryHouseholdId);
        Assert.Same(query, repo.LastQueryDto);
        Assert.Equal(1, repo.LastPage);
        Assert.Equal(100, repo.LastPageSize);
    }

    [Fact]
    public async Task GetInventoryStatsAsync_ReturnsNoActiveHousehold_WhenNone()
    {
        var householdService = new FakeHouseholdService { ActiveHouseholdId = null };
        var repo = new FakeInventoryRepository();
        var service = CreateService(householdService, repo);

        var result = await service.GetInventoryStatsAsync("clerk_123", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(InventoryError.NoActiveHousehold, result.Result);
        Assert.False(repo.StatsCalled);
    }

    [Fact]
    public async Task GetInventoryStatsAsync_ReturnsStats_WhenAuthorized()
    {
        var householdService = new FakeHouseholdService { ActiveHouseholdId = HouseholdId };
        var repo = new FakeInventoryRepository
        {
            StatsToReturn = new InventoryStatsResponseDto(10, 3, 3)
        };
        var service = CreateService(householdService, repo);

        var result = await service.GetInventoryStatsAsync("clerk_123", CancellationToken.None);

        Assert.True(result.IsSuccess);
        var dto = Assert.IsType<InventoryStatsResponseDto>(result.Data);
        Assert.Equal(10, dto.TotalCount);
        Assert.Equal(3, dto.ExpiringSoonCount);
        Assert.Equal(3, dto.StorageMethodCount);
        Assert.True(repo.StatsCalled);
        Assert.Equal(HouseholdId, repo.LastStatsHouseholdId);
    }

    private static InventoryService CreateService(
        IHouseholdService householdService,
        IInventoryRepository repository,
        ISmartRecipeService? smartRecipeService = null)
    {
        var mockDbContext = new Mock<AppDbContext>(new DbContextOptions<AppDbContext>());
        return new InventoryService(
            householdService,
            repository,
            smartRecipeService ?? Mock.Of<ISmartRecipeService>(),
            mockDbContext.Object,
            NullLogger<InventoryService>.Instance);
    }

    private static CreateInventoryItemRequestDto NewCreateRequest(int? expirationDays = 3) => new()
    {
        Name = "Tomatoes",
        Amount = 2,
        Unit = "pcs",
        StorageMethod = InventoryStorageMethod.RoomTemp,
        ExpirationDays = expirationDays
    };

    private static List<CreateInventoryItemRequestDto> NewBatchRequests() =>
        [NewCreateRequest(), NewCreateRequest(expirationDays: 5)];

    private static UpdateInventoryItemRequestDto NewUpdateRequest(int? expirationDays = 3, decimal amount = 2, string unit = "pcs", InventoryStorageMethod storage = InventoryStorageMethod.RoomTemp) => new()
    {
        Amount = amount,
        Unit = unit,
        StorageMethod = storage,
        ExpirationDays = expirationDays
    };

    private static InventoryListRequestDto NewListRequest(int page = 1, int pageSize = 10) => new()
    {
        Keyword = "tomato",
        StorageMethod = InventoryStorageMethod.RoomTemp,
        SortBy = InventorySortBy.DateAdded,
        SortOrder = SortOrder.Asc,
        Page = page,
        PageSize = pageSize
    };

    private static InventoryItem NewInventoryItem() => NewInventoryItem(InventoryItemId, "Tomatoes");

    private static InventoryItem NewInventoryItem(Guid id, string name) => new()
    {
        Id = id,
        HouseholdId = HouseholdId,
        IngredientId = IngredientId,
        Name = name,
        Amount = 1,
        Unit = "pcs",
        StorageMethod = InventoryStorageMethod.RoomTemp,
        Status = InventoryItemStatus.Active,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private sealed class FakeHouseholdService : IHouseholdService
    {
        public Guid? ActiveHouseholdId { get; set; }

        public Task<HouseholdLeaveResult> LeaveHouseholdAsync(Guid householdId, string clerkUserId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<HouseholdMembershipListDto?> GetMembershipsAsync(string clerkUserId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<Guid?> GetActiveHouseholdIdAsync(string clerkUserId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ActiveHouseholdId);
        }

        public Task<HouseholdMembersResult> GetHouseholdMembersAsync(Guid householdId, string clerkUserId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<RemoveMemberResult> RemoveMemberAsync(Guid householdId, Guid memberId, string clerkUserId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }

    private sealed class FakeInventoryRepository : IInventoryRepository
    {
        public InventoryItem? LastAdded { get; private set; }
        public List<InventoryItem> LastAddedBatch { get; } = [];
        public InventoryItem? LastUpdated { get; private set; }
        public InventoryItem? Item { get; set; }
        public bool UpdateCalled { get; private set; }
        public bool DeleteResult { get; set; } = true;
        public bool DeleteCalled { get; private set; }
        public Guid? LastDeleteId { get; private set; }
        public bool QueryCalled { get; private set; }
        public Guid? LastQueryHouseholdId { get; private set; }
        public InventoryListRequestDto? LastQueryDto { get; private set; }
        public int LastPage { get; private set; }
        public int LastPageSize { get; private set; }
        public List<InventoryItem> QueryItems { get; set; } = [];
        public int QueryTotalCount { get; set; }
        public bool StatsCalled { get; private set; }
        public Guid? LastStatsHouseholdId { get; private set; }
        public InventoryStatsResponseDto? StatsToReturn { get; set; }

        public Task<InventoryItem> AddAsync(InventoryItem item, CancellationToken cancellationToken = default)
        {
            LastAdded = item;
            Item = item;
            return Task.FromResult(item);
        }

        public Task AddRangeAsync(IEnumerable<InventoryItem> items, CancellationToken cancellationToken = default)
        {
            LastAddedBatch.Clear();
            LastAddedBatch.AddRange(items);
            return Task.CompletedTask;
        }

        public Task<InventoryItem?> GetByIdAsync(Guid itemId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Item is not null && Item.Id == itemId ? Item : null);
        }

        public Task<InventoryItem> UpdateAsync(InventoryItem item, CancellationToken cancellationToken = default)
        {
            UpdateCalled = true;
            LastUpdated = item;
            Item = item;
            return Task.FromResult(item);
        }

        public Task<bool> DeleteAsync(Guid itemId, CancellationToken cancellationToken = default)
        {
            DeleteCalled = true;
            LastDeleteId = itemId;
            return Task.FromResult(DeleteResult);
        }

        public Task<(IReadOnlyList<InventoryItem>, int)> QueryAsync(
            Guid householdId,
            InventoryListRequestDto query,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
        {
            QueryCalled = true;
            LastQueryHouseholdId = householdId;
            LastQueryDto = query;
            LastPage = page;
            LastPageSize = pageSize;
            return Task.FromResult(((IReadOnlyList<InventoryItem>)QueryItems, QueryTotalCount));
        }

        public Task<InventoryStatsResponseDto> GetStatsAsync(
            Guid householdId,
            CancellationToken cancellationToken)
        {
            StatsCalled = true;
            LastStatsHouseholdId = householdId;
            return Task.FromResult(StatsToReturn ?? new InventoryStatsResponseDto(0, 0, 0));
        }
    }
}
