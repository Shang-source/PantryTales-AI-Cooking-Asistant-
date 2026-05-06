using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using backend.Data;
using backend.Interfaces;
using backend.Models;
using backend.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Pgvector;
using Xunit;

namespace backend.Tests.Services;

public class RecipeLikeServiceTests
{
    private static readonly Guid RecipeId = Guid.Parse("00000000-0000-0000-0000-000000000999");

    [Fact]
    public async Task ToggleLikeAsync_ReturnsNull_WhenUserNotFound()
    {
        var likeRepo = new FakeRecipeLikeRepository();
        var recipeRepo = new FakeRecipeRepository { Recipe = new Recipe { Id = RecipeId } };
        var userRepo = new FakeUserRepository();
        await using var fixture = CreateFixture(likeRepo, recipeRepo, userRepo);
        var service = fixture.Service;

        var result = await service.ToggleLikeAsync(RecipeId, "missing", CancellationToken.None);

        Assert.Null(result);
        Assert.False(likeRepo.AddCalled);
        Assert.False(likeRepo.RemoveCalled);
        Assert.Equal(0, likeRepo.SaveChangesCount);
        Assert.Equal(0, likeRepo.IncrementCalls);
        Assert.Equal(0, likeRepo.DecrementCalls);
    }

    [Fact]
    public async Task ToggleLikeAsync_ReturnsNull_WhenRecipeNotFound()
    {
        var likeRepo = new FakeRecipeLikeRepository();
        var recipeRepo = new FakeRecipeRepository();
        var userRepo = new FakeUserRepository { User = new User { Id = Guid.NewGuid() } };
        await using var fixture = CreateFixture(likeRepo, recipeRepo, userRepo);
        var service = fixture.Service;

        var result = await service.ToggleLikeAsync(RecipeId, "clerk_abc", CancellationToken.None);

        Assert.Null(result);
        Assert.False(likeRepo.AddCalled);
        Assert.False(likeRepo.RemoveCalled);
        Assert.Equal(0, likeRepo.SaveChangesCount);
        Assert.Equal(0, likeRepo.IncrementCalls);
        Assert.Equal(0, likeRepo.DecrementCalls);
    }

    [Fact]
    public async Task ToggleLikeAsync_CreatesLike_WhenNoneExists()
    {
        var user = new User { Id = Guid.NewGuid() };
        var recipe = new Recipe { Id = RecipeId, LikesCount = 2, UpdatedAt = DateTime.UtcNow.AddMinutes(-5) };
        var likeRepo = new FakeRecipeLikeRepository { IncrementedCount = 3 };
        var recipeRepo = new FakeRecipeRepository { Recipe = recipe };
        var userRepo = new FakeUserRepository { User = user };
        await using var fixture = CreateFixture(likeRepo, recipeRepo, userRepo);
        var service = fixture.Service;
        var originalUpdatedAt = recipe.UpdatedAt;

        var response = await service.ToggleLikeAsync(RecipeId, "clerk_user", CancellationToken.None);

        Assert.NotNull(response);
        Assert.True(response!.IsLiked);
        Assert.Equal(RecipeId, response.RecipeId);
        Assert.Equal(3, response.LikesCount);
        Assert.True(likeRepo.AddCalled);
        Assert.False(likeRepo.RemoveCalled);
        Assert.Equal(1, likeRepo.SaveChangesCount);
        Assert.Equal(1, likeRepo.IncrementCalls);
        Assert.Equal(0, likeRepo.DecrementCalls);
        Assert.NotNull(likeRepo.AddedLike);
        Assert.Equal(user.Id, likeRepo.AddedLike!.UserId);
        Assert.Equal(recipe.Id, likeRepo.AddedLike.RecipeId);
        Assert.True(recipe.UpdatedAt > originalUpdatedAt);
    }

    [Fact]
    public async Task ToggleLikeAsync_RemovesLike_AndDoesNotGoNegative()
    {
        var user = new User { Id = Guid.NewGuid() };
        var recipe = new Recipe { Id = RecipeId, LikesCount = 1, UpdatedAt = DateTime.UtcNow.AddMinutes(-10) };
        var existingLike = new RecipeLike { UserId = user.Id, RecipeId = RecipeId, CreatedAt = DateTime.UtcNow };
        var likeRepo = new FakeRecipeLikeRepository { ExistingLike = existingLike, DecrementedCount = 0 };
        var recipeRepo = new FakeRecipeRepository { Recipe = recipe };
        var userRepo = new FakeUserRepository { User = user };
        await using var fixture = CreateFixture(likeRepo, recipeRepo, userRepo);
        var service = fixture.Service;
        var originalUpdatedAt = recipe.UpdatedAt;

        var response = await service.ToggleLikeAsync(RecipeId, "clerk_user", CancellationToken.None);

        Assert.NotNull(response);
        Assert.False(response!.IsLiked);
        Assert.Equal(RecipeId, response.RecipeId);
        Assert.Equal(0, response.LikesCount);
        Assert.False(likeRepo.AddCalled);
        Assert.True(likeRepo.RemoveCalled);
        Assert.Equal(1, likeRepo.SaveChangesCount);
        Assert.Equal(0, likeRepo.IncrementCalls);
        Assert.Equal(1, likeRepo.DecrementCalls);
        Assert.Same(existingLike, likeRepo.RemovedLike);
        Assert.True(recipe.UpdatedAt > originalUpdatedAt);
    }

    private static ServiceFixture CreateFixture(
        FakeRecipeLikeRepository likeRepo,
        FakeRecipeRepository recipeRepo,
        FakeUserRepository userRepo)
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var dbContext = new TestDbContext(options, connection);
        dbContext.Database.EnsureCreated();

        var service = new RecipeLikeService(dbContext, likeRepo, recipeRepo, userRepo, NullLogger<RecipeLikeService>.Instance);
        return new ServiceFixture(service, dbContext, connection);
    }

    [Fact]
    public async Task GetMyLikedRecipesAsync_ExcludesPrivateRecipes_EvenIfAuthoredByUser()
    {
        var user = new User { Id = Guid.NewGuid(), ClerkUserId = "clerk_user", Nickname = "User", Email = "u@example.com" };
        var publicRecipe = new Recipe
        {
            Id = Guid.NewGuid(),
            HouseholdId = Guid.NewGuid(),
            AuthorId = user.Id,
            Title = "Public",
            Description = "d",
            Steps = "[]",
            Type = RecipeType.User,
            Visibility = RecipeVisibility.Public,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            UpdatedAt = DateTime.UtcNow.AddDays(-2)
        };
        var privateRecipe = new Recipe
        {
            Id = Guid.NewGuid(),
            HouseholdId = publicRecipe.HouseholdId,
            AuthorId = user.Id,
            Title = "Private",
            Description = "d",
            Steps = "[]",
            Type = RecipeType.User,
            Visibility = RecipeVisibility.Private,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };

        var likeRepo = new FakeRecipeLikeRepository();
        var recipeRepo = new FakeRecipeRepository();
        var userRepo = new FakeUserRepository { User = user };
        await using var fixture = CreateFixture(likeRepo, recipeRepo, userRepo);

        fixture.DbContext.Users.Add(user);
        fixture.DbContext.Recipes.AddRange(publicRecipe, privateRecipe);
        fixture.DbContext.RecipeLikes.AddRange(
            new RecipeLike { UserId = user.Id, RecipeId = publicRecipe.Id, CreatedAt = DateTime.UtcNow.AddHours(-2) },
            new RecipeLike { UserId = user.Id, RecipeId = privateRecipe.Id, CreatedAt = DateTime.UtcNow.AddHours(-1) });
        await fixture.DbContext.SaveChangesAsync();

        var results = await fixture.Service.GetMyLikedRecipesAsync(user.Id, page: 1, pageSize: 20, CancellationToken.None);

        Assert.NotNull(results);
        Assert.Single(results!);
        Assert.Equal(publicRecipe.Id, results![0].Id);
    }

    [Fact]
    public async Task GetMyLikesCountAsync_ExcludesPrivateRecipes()
    {
        var user = new User { Id = Guid.NewGuid(), ClerkUserId = "clerk_user", Nickname = "User", Email = "u@example.com" };
        var publicRecipe = new Recipe
        {
            Id = Guid.NewGuid(),
            HouseholdId = Guid.NewGuid(),
            AuthorId = user.Id,
            Title = "Public",
            Description = "d",
            Steps = "[]",
            Type = RecipeType.User,
            Visibility = RecipeVisibility.Public,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            UpdatedAt = DateTime.UtcNow.AddDays(-2)
        };
        var privateRecipe = new Recipe
        {
            Id = Guid.NewGuid(),
            HouseholdId = publicRecipe.HouseholdId,
            AuthorId = user.Id,
            Title = "Private",
            Description = "d",
            Steps = "[]",
            Type = RecipeType.User,
            Visibility = RecipeVisibility.Private,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };

        var likeRepo = new FakeRecipeLikeRepository();
        var recipeRepo = new FakeRecipeRepository();
        var userRepo = new FakeUserRepository { User = user };
        await using var fixture = CreateFixture(likeRepo, recipeRepo, userRepo);

        fixture.DbContext.Users.Add(user);
        fixture.DbContext.Recipes.AddRange(publicRecipe, privateRecipe);
        fixture.DbContext.RecipeLikes.AddRange(
            new RecipeLike { UserId = user.Id, RecipeId = publicRecipe.Id, CreatedAt = DateTime.UtcNow.AddHours(-2) },
            new RecipeLike { UserId = user.Id, RecipeId = privateRecipe.Id, CreatedAt = DateTime.UtcNow.AddHours(-1) });
        await fixture.DbContext.SaveChangesAsync();

        var count = await fixture.Service.GetMyLikesCountAsync("clerk_user", CancellationToken.None);

        Assert.Equal(1, count);
    }

    private sealed class ServiceFixture(RecipeLikeService service, AppDbContext dbContext, SqliteConnection connection)
        : IAsyncDisposable
    {
        public RecipeLikeService Service { get; } = service;
        public AppDbContext DbContext { get; } = dbContext;

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class FakeRecipeLikeRepository : IRecipeLikeRepository
    {
        public RecipeLike? ExistingLike { get; set; }
        public int IncrementedCount { get; set; }
        public int DecrementedCount { get; set; }
        public bool AddCalled { get; private set; }
        public bool RemoveCalled { get; private set; }
        public int SaveChangesCount { get; private set; }
        public int IncrementCalls { get; private set; }
        public int DecrementCalls { get; private set; }
        public RecipeLike? AddedLike { get; private set; }
        public RecipeLike? RemovedLike { get; private set; }

        public Task<RecipeLike?> GetRecipeLikeAsync(Guid userId, Guid recipeId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ExistingLike);
        }

        public Task AddRecipeLikeAsync(RecipeLike recipeLike, CancellationToken cancellationToken = default)
        {
            AddCalled = true;
            AddedLike = recipeLike;
            ExistingLike = recipeLike;
            return Task.CompletedTask;
        }

        public Task RemoveRecipeLikeAsync(RecipeLike recipeLike, CancellationToken cancellationToken = default)
        {
            RemoveCalled = true;
            RemovedLike = recipeLike;
            ExistingLike = null;
            return Task.CompletedTask;
        }

        public Task<int> IncrementRecipeLikesCountAsync(Guid recipeId, DateTime updatedAt,
            CancellationToken cancellationToken = default)
        {
            IncrementCalls++;
            return Task.FromResult(IncrementedCount);
        }

        public Task<int> DecrementRecipeLikesCountAsync(Guid recipeId, DateTime updatedAt,
            CancellationToken cancellationToken = default)
        {
            DecrementCalls++;
            return Task.FromResult(DecrementedCount);
        }

        public Task<int?> GetRecipeLikesCountAsync(Guid recipeId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<int?>(IncrementedCount);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRecipeRepository : IRecipeRepository
    {
        public Recipe? Recipe { get; set; }

        public Task<Recipe?> GetByIdAsync(Guid recipeId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Recipe);
        }

        public Task<Recipe?> GetDetailedByIdAsync(Guid recipeId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Recipe);
        }

        public Task<List<Recipe>> GetFeaturedRecipesAsync(int count, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<Recipe>());

        public Task ToggleFeaturedAsync(Guid recipeId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public User? User { get; set; }

        public Task<User?> GetByClerkUserIdAsync(string clerkUserId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(User);
        }

        public Task<User> AddAsync(User user, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<User> UpdateAsync(User user, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }

    private sealed class TestDbContext : AppDbContext
    {
        private readonly SqliteConnection _connection;

        public TestDbContext(DbContextOptions<AppDbContext> options, SqliteConnection connection) : base(options)
        {
            _connection = connection;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Ignore<Vector>();
            modelBuilder.Ignore<Tag>();
            modelBuilder.Ignore<TagType>();
            modelBuilder.Ignore<Ingredient>();
            modelBuilder.Ignore<IngredientAlias>();
            modelBuilder.Ignore<IngredientUnit>();
            modelBuilder.Ignore<IngredientTag>();
            modelBuilder.Ignore<InventoryItem>();
            modelBuilder.Ignore<InventoryItemTag>();
            modelBuilder.Ignore<backend.Models.RecipeIngredient>();
            modelBuilder.Ignore<RecipeIngredientTag>();
            modelBuilder.Ignore<RecipeTag>();
            modelBuilder.Ignore<RecipeSave>();
            modelBuilder.Ignore<RecipeComment>();
            modelBuilder.Ignore<RecipeInteraction>();
            modelBuilder.Ignore<ChecklistItem>();
            modelBuilder.Ignore<KnowledgebaseArticle>();
            modelBuilder.Ignore<Household>();
            modelBuilder.Ignore<HouseholdMember>();
            modelBuilder.Ignore<HouseholdInvitation>();
            modelBuilder.Ignore<UserPreference>();
            modelBuilder.Ignore<NameNormalizationToken>();
            modelBuilder.Ignore<NameNormalizationDictionaryVersion>();

            modelBuilder.Entity<Recipe>()
                .HasKey(r => r.Id);
            modelBuilder.Entity<Recipe>()
                .Ignore(r => r.Embedding);
            modelBuilder.Entity<User>()
                .HasKey(u => u.Id);
            modelBuilder.Entity<User>()
                .Ignore(u => u.Embedding);
            modelBuilder.Entity<RecipeLike>()
                .HasKey(rl => new { rl.UserId, rl.RecipeId });
        }

        public override void Dispose()
        {
            base.Dispose();
            _connection.Dispose();
        }
    }
}
