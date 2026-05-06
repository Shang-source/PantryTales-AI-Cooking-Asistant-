using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using backend.Data;
using backend.Dtos.Checklist;
using backend.Dtos.Households;
using backend.Interfaces;
using backend.Models;
using backend.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace backend.Tests.Services;

public class ChecklistServiceTests
{
    private static readonly Guid HouseholdId = Guid.Parse("00000000-0000-0000-0000-000000000111");
    private static readonly Guid ChecklistItemId = Guid.Parse("00000000-0000-0000-0000-000000000222");
    private static readonly Guid RecipeId = Guid.Parse("00000000-0000-0000-0000-000000000333");

    #region ClearAllAsync Tests

    [Fact]
    public async Task ClearAllAsync_ReturnsNoActiveHousehold_WhenNoneExists()
    {
        var householdService = new FakeHouseholdService { ActiveHouseholdId = null };
        var repo = new FakeChecklistRepository();
        var service = CreateService(householdService, repo);

        var result = await service.ClearAllAsync("clerk_123", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ChecklistError.NoActiveHousehold, result.Error);
        Assert.False(repo.DeleteAllCalled);
    }

    [Fact]
    public async Task ClearAllAsync_ReturnsDeletedCount_WhenSucceeds()
    {
        var householdService = new FakeHouseholdService { ActiveHouseholdId = HouseholdId };
        var repo = new FakeChecklistRepository { DeleteAllResult = 5 };
        var service = CreateService(householdService, repo);

        var result = await service.ClearAllAsync("clerk_123", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Data);
        Assert.True(repo.DeleteAllCalled);
        Assert.Equal(HouseholdId, repo.LastDeleteAllHouseholdId);
    }

    [Fact]
    public async Task ClearAllAsync_ReturnsZero_WhenListEmpty()
    {
        var householdService = new FakeHouseholdService { ActiveHouseholdId = HouseholdId };
        var repo = new FakeChecklistRepository { DeleteAllResult = 0 };
        var service = CreateService(householdService, repo);

        var result = await service.ClearAllAsync("clerk_123", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Data);
        Assert.True(repo.DeleteAllCalled);
    }

    #endregion

    #region ClearCheckedAsync Tests

    [Fact]
    public async Task ClearCheckedAsync_ReturnsNoActiveHousehold_WhenNoneExists()
    {
        var householdService = new FakeHouseholdService { ActiveHouseholdId = null };
        var repo = new FakeChecklistRepository();
        var service = CreateService(householdService, repo);

        var result = await service.ClearCheckedAsync("clerk_123", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ChecklistError.NoActiveHousehold, result.Error);
        Assert.False(repo.DeleteCheckedCalled);
    }

    [Fact]
    public async Task ClearCheckedAsync_ReturnsDeletedCount_WhenSucceeds()
    {
        var householdService = new FakeHouseholdService { ActiveHouseholdId = HouseholdId };
        var repo = new FakeChecklistRepository { DeleteCheckedResult = 3 };
        var service = CreateService(householdService, repo);

        var result = await service.ClearCheckedAsync("clerk_123", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Data);
        Assert.True(repo.DeleteCheckedCalled);
        Assert.Equal(HouseholdId, repo.LastDeleteCheckedHouseholdId);
    }

    #endregion

    #region GetItemsAsync Tests

    [Fact]
    public async Task GetItemsAsync_ReturnsNoActiveHousehold_WhenNoneExists()
    {
        var householdService = new FakeHouseholdService { ActiveHouseholdId = null };
        var repo = new FakeChecklistRepository();
        var service = CreateService(householdService, repo);

        var result = await service.GetItemsAsync("clerk_123", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ChecklistError.NoActiveHousehold, result.Error);
    }

    [Fact]
    public async Task GetItemsAsync_ReturnsItems_WhenAuthorized()
    {
        var householdService = new FakeHouseholdService { ActiveHouseholdId = HouseholdId };
        var repo = new FakeChecklistRepository
        {
            Items = [NewChecklistItem(), NewChecklistItem(Guid.NewGuid(), "Onions")]
        };
        var service = CreateService(householdService, repo);

        var result = await service.GetItemsAsync("clerk_123", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Items.Count);
        Assert.Equal(2, result.Data.TotalCount);
    }

    #endregion

    #region AddItemAsync Tests

    [Fact]
    public async Task AddItemAsync_ReturnsNoActiveHousehold_WhenNoneExists()
    {
        var householdService = new FakeHouseholdService { ActiveHouseholdId = null };
        var repo = new FakeChecklistRepository();
        var service = CreateService(householdService, repo);
        var dto = new CreateChecklistItemDto("Tomatoes", 2, "pcs", "Vegetables");

        var result = await service.AddItemAsync("clerk_123", dto, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ChecklistError.NoActiveHousehold, result.Error);
        Assert.Null(repo.LastAddedItem);
    }

    [Fact]
    public async Task AddItemAsync_ReturnsCreatedItem_WhenSucceeds()
    {
        var householdService = new FakeHouseholdService { ActiveHouseholdId = HouseholdId };
        var repo = new FakeChecklistRepository();
        var service = CreateService(householdService, repo);
        var dto = new CreateChecklistItemDto("Tomatoes", 2, "pcs", "Vegetables");

        var result = await service.AddItemAsync("clerk_123", dto, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal("Tomatoes", result.Data.Name);
        Assert.Equal(2m, result.Data.Amount);
        Assert.Equal("pcs", result.Data.Unit);
        Assert.Equal("Vegetables", result.Data.Category);
        Assert.False(result.Data.IsChecked);
        Assert.NotNull(repo.LastAddedItem);
        Assert.Equal(HouseholdId, repo.LastAddedItem.HouseholdId);
    }

    #endregion

    #region DeleteItemAsync Tests

    [Fact]
    public async Task DeleteItemAsync_ReturnsItemNotFound_WhenMissing()
    {
        var householdService = new FakeHouseholdService { ActiveHouseholdId = HouseholdId };
        var repo = new FakeChecklistRepository { Item = null };
        var service = CreateService(householdService, repo);

        var result = await service.DeleteItemAsync(ChecklistItemId, "clerk_123", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ChecklistError.ItemNotFound, result.Error);
        Assert.False(repo.DeleteCalled);
    }

    [Fact]
    public async Task DeleteItemAsync_ReturnsNoActiveHousehold_WhenNone()
    {
        var householdService = new FakeHouseholdService { ActiveHouseholdId = null };
        var repo = new FakeChecklistRepository { Item = NewChecklistItem() };
        var service = CreateService(householdService, repo);

        var result = await service.DeleteItemAsync(ChecklistItemId, "clerk_123", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ChecklistError.NoActiveHousehold, result.Error);
        Assert.False(repo.DeleteCalled);
    }

    [Fact]
    public async Task DeleteItemAsync_ReturnsHouseholdMismatch_WhenDifferentHousehold()
    {
        var householdService = new FakeHouseholdService { ActiveHouseholdId = Guid.Parse("00000000-0000-0000-0000-000000000001") };
        var repo = new FakeChecklistRepository { Item = NewChecklistItem() };
        var service = CreateService(householdService, repo);

        var result = await service.DeleteItemAsync(ChecklistItemId, "clerk_123", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ChecklistError.HouseholdMismatch, result.Error);
        Assert.False(repo.DeleteCalled);
    }

    [Fact]
    public async Task DeleteItemAsync_ReturnsOk_WhenDeleted()
    {
        var householdService = new FakeHouseholdService { ActiveHouseholdId = HouseholdId };
        var repo = new FakeChecklistRepository { Item = NewChecklistItem(), DeleteResult = true };
        var service = CreateService(householdService, repo);

        var result = await service.DeleteItemAsync(ChecklistItemId, "clerk_123", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(repo.DeleteCalled);
        Assert.Equal(ChecklistItemId, repo.LastDeleteId);
    }

    #endregion

    #region GetStatsAsync Tests

    [Fact]
    public async Task GetStatsAsync_ReturnsNoActiveHousehold_WhenNoneExists()
    {
        var householdService = new FakeHouseholdService { ActiveHouseholdId = null };
        var repo = new FakeChecklistRepository();
        var service = CreateService(householdService, repo);

        var result = await service.GetStatsAsync("clerk_123", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ChecklistError.NoActiveHousehold, result.Error);
    }

    [Fact]
    public async Task GetStatsAsync_ReturnsStats_WhenAuthorized()
    {
        var householdService = new FakeHouseholdService { ActiveHouseholdId = HouseholdId };
        var repo = new FakeChecklistRepository
        {
            CountResult = 10,
            CheckedCountResult = 3
        };
        var service = CreateService(householdService, repo);

        var result = await service.GetStatsAsync("clerk_123", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(10, result.Data.TotalCount);
        Assert.Equal(3, result.Data.PurchasedCount);
        Assert.Equal(7, result.Data.RemainingCount);
    }

    #endregion

    private static ChecklistService CreateService(
        IHouseholdService householdService,
        IChecklistRepository repository)
    {
        var mockDbContext = new Mock<AppDbContext>(new DbContextOptions<AppDbContext>());
        return new ChecklistService(
            householdService,
            repository,
            mockDbContext.Object,
            Mock.Of<ISmartRecipeService>(),
            NullLogger<ChecklistService>.Instance);
    }

    private static ChecklistItem NewChecklistItem() => NewChecklistItem(ChecklistItemId, "Tomatoes");

    private static ChecklistItem NewChecklistItem(Guid id, string name) => new()
    {
        Id = id,
        HouseholdId = HouseholdId,
        Name = name,
        Amount = 1,
        Unit = "pcs",
        Category = "Vegetables",
        IsChecked = false,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private sealed class FakeHouseholdService : IHouseholdService
    {
        public Guid? ActiveHouseholdId { get; set; }

        public Task<HouseholdLeaveResult> LeaveHouseholdAsync(Guid householdId, string clerkUserId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<HouseholdMembershipListDto?> GetMembershipsAsync(string clerkUserId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<Guid?> GetActiveHouseholdIdAsync(string clerkUserId, CancellationToken cancellationToken = default)
            => Task.FromResult(ActiveHouseholdId);

        public Task<HouseholdMembersResult> GetHouseholdMembersAsync(Guid householdId, string clerkUserId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<RemoveMemberResult> RemoveMemberAsync(Guid householdId, Guid memberId, string clerkUserId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class FakeChecklistRepository : IChecklistRepository
    {
        public List<ChecklistItem> Items { get; set; } = [];
        public ChecklistItem? Item { get; set; }
        public ChecklistItem? LastAddedItem { get; private set; }
        public List<ChecklistItem> LastAddedItems { get; } = [];
        public ChecklistItem? LastUpdatedItem { get; private set; }
        public bool DeleteCalled { get; private set; }
        public Guid? LastDeleteId { get; private set; }
        public bool DeleteResult { get; set; } = true;
        public bool DeleteCheckedCalled { get; private set; }
        public Guid? LastDeleteCheckedHouseholdId { get; private set; }
        public int DeleteCheckedResult { get; set; }
        public bool DeleteAllCalled { get; private set; }
        public Guid? LastDeleteAllHouseholdId { get; private set; }
        public int DeleteAllResult { get; set; }
        public int CountResult { get; set; }
        public int CheckedCountResult { get; set; }

        public Task<List<ChecklistItem>> GetByHouseholdAsync(Guid householdId, CancellationToken cancellationToken = default)
            => Task.FromResult(Items);

        public Task<int> GetCountByHouseholdAsync(Guid householdId, CancellationToken cancellationToken = default)
            => Task.FromResult(CountResult);

        public Task<int> GetCheckedCountByHouseholdAsync(Guid householdId, CancellationToken cancellationToken = default)
            => Task.FromResult(CheckedCountResult);

        public Task<ChecklistItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(Item is not null && Item.Id == id ? Item : null);

        public Task AddAsync(ChecklistItem item, CancellationToken cancellationToken = default)
        {
            LastAddedItem = item;
            return Task.CompletedTask;
        }

        public Task AddRangeAsync(List<ChecklistItem> items, CancellationToken cancellationToken = default)
        {
            LastAddedItems.Clear();
            LastAddedItems.AddRange(items);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(ChecklistItem item, CancellationToken cancellationToken = default)
        {
            LastUpdatedItem = item;
            return Task.CompletedTask;
        }

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            DeleteCalled = true;
            LastDeleteId = id;
            return Task.FromResult(DeleteResult);
        }

        public Task<int> DeleteCheckedAsync(Guid householdId, CancellationToken cancellationToken = default)
        {
            DeleteCheckedCalled = true;
            LastDeleteCheckedHouseholdId = householdId;
            return Task.FromResult(DeleteCheckedResult);
        }

        public Task<int> DeleteAllAsync(Guid householdId, CancellationToken cancellationToken = default)
        {
            DeleteAllCalled = true;
            LastDeleteAllHouseholdId = householdId;
            return Task.FromResult(DeleteAllResult);
        }
    }
}
