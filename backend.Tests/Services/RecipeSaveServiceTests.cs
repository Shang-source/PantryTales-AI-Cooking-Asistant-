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

public class RecipeSaveServiceTests
{
    [Fact]
    public async Task GetMySavedRecipesAsync_ExcludesPrivateRecipes_EvenIfAuthoredByUser()
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

        var saveRepo = new FakeRecipeSaveRepository();
        var recipeRepo = new FakeRecipeRepository();
        var userRepo = new FakeUserRepository { User = user };
        await using var fixture = CreateFixture(saveRepo, recipeRepo, userRepo);

        fixture.DbContext.Users.Add(user);
        fixture.DbContext.Recipes.AddRange(publicRecipe, privateRecipe);
        fixture.DbContext.RecipeSaves.AddRange(
            new RecipeSave { UserId = user.Id, RecipeId = publicRecipe.Id, CreatedAt = DateTime.UtcNow.AddHours(-2) },
            new RecipeSave { UserId = user.Id, RecipeId = privateRecipe.Id, CreatedAt = DateTime.UtcNow.AddHours(-1) });
        await fixture.DbContext.SaveChangesAsync();

        var results = await fixture.Service.GetMySavedRecipesAsync(user.Id, page: 1, pageSize: 20, CancellationToken.None);

        Assert.NotNull(results);
        Assert.Single(results!);
        Assert.Equal(publicRecipe.Id, results![0].Id);
    }

    [Fact]
    public async Task GetMySavesCountAsync_ExcludesPrivateRecipes()
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

        var saveRepo = new FakeRecipeSaveRepository();
        var recipeRepo = new FakeRecipeRepository();
        var userRepo = new FakeUserRepository { User = user };
        await using var fixture = CreateFixture(saveRepo, recipeRepo, userRepo);

        fixture.DbContext.Users.Add(user);
        fixture.DbContext.Recipes.AddRange(publicRecipe, privateRecipe);
        fixture.DbContext.RecipeSaves.AddRange(
            new RecipeSave { UserId = user.Id, RecipeId = publicRecipe.Id, CreatedAt = DateTime.UtcNow.AddHours(-2) },
            new RecipeSave { UserId = user.Id, RecipeId = privateRecipe.Id, CreatedAt = DateTime.UtcNow.AddHours(-1) });
        await fixture.DbContext.SaveChangesAsync();

        var count = await fixture.Service.GetMySavesCountAsync("clerk_user", CancellationToken.None);

        Assert.Equal(1, count);
    }

    private static ServiceFixture CreateFixture(
        FakeRecipeSaveRepository saveRepo,
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

        var service = new RecipeSaveService(
            dbContext,
            saveRepo,
            recipeRepo,
            userRepo,
            NullLogger<RecipeSaveService>.Instance);
        return new ServiceFixture(service, dbContext, connection);
    }

    private sealed class ServiceFixture(RecipeSaveService service, AppDbContext dbContext, SqliteConnection connection)
        : IAsyncDisposable
    {
        public RecipeSaveService Service { get; } = service;
        public AppDbContext DbContext { get; } = dbContext;

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class FakeRecipeSaveRepository : IRecipeSaveRepository
    {
        public Task<RecipeSave?> GetRecipeSaveAsync(Guid userId, Guid recipeId, CancellationToken cancellationToken = default)
            => Task.FromResult<RecipeSave?>(null);

        public Task AddRecipeSaveAsync(RecipeSave recipeSave, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RemoveRecipeSaveAsync(RecipeSave recipeSave, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<int> IncrementRecipeSavedCountAsync(Guid recipeId, DateTime updatedAt, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<int> DecrementRecipeSavedCountAsync(Guid recipeId, DateTime updatedAt, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<int?> GetRecipeSavedCountAsync(Guid recipeId, CancellationToken cancellationToken = default)
            => Task.FromResult<int?>(0);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeRecipeRepository : IRecipeRepository
    {
        public Task<Recipe?> GetByIdAsync(Guid recipeId, CancellationToken cancellationToken = default)
            => Task.FromResult<Recipe?>(null);

        public Task<Recipe?> GetDetailedByIdAsync(Guid recipeId, CancellationToken cancellationToken = default)
            => Task.FromResult<Recipe?>(null);

        public Task<List<Recipe>> GetFeaturedRecipesAsync(int count, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<Recipe>());

        public Task ToggleFeaturedAsync(Guid recipeId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public User? User { get; set; }

        public Task<User?> GetByClerkUserIdAsync(string clerkUserId, CancellationToken cancellationToken = default)
            => Task.FromResult(User);

        public Task<User> AddAsync(User user, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<User> UpdateAsync(User user, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
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
            modelBuilder.Ignore<RecipeLike>();
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
            modelBuilder.Entity<RecipeSave>()
                .HasKey(rs => new { rs.UserId, rs.RecipeId });
        }

        public override void Dispose()
        {
            base.Dispose();
            _connection.Dispose();
        }
    }
}

