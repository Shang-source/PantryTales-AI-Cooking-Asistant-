using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using backend.Data;
using backend.Dtos.Interactions;
using backend.Interfaces;
using backend.Models;
using backend.Repository;
using backend.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace backend.Tests.Services;

public class RecipeInteractionServiceTests
{
    private const string ClerkUserId = "clerk_test_user";

    [Fact]
    public async Task LogInteractionAsync_ReturnsFalse_WhenUserNotFound()
    {
        await using var context = CreateContext();
        var repo = new RecipeInteractionRepository(context);
        var service = CreateService(context, repo);

        var result = await service.LogInteractionAsync(
            "nonexistent_user",
            Guid.NewGuid(),
            RecipeInteractionEventType.Click,
            cancellationToken: CancellationToken.None);

        Assert.False(result);
        Assert.Empty(context.RecipeInteractions);
    }

    [Fact]
    public async Task LogInteractionAsync_ReturnsFalse_WhenRecipeNotFound_ForNonImpressionEvents()
    {
        await using var context = CreateContext();
        SeedUserAndRecipe(context);
        var repo = new RecipeInteractionRepository(context);
        var service = CreateService(context, repo);

        var result = await service.LogInteractionAsync(
            ClerkUserId,
            Guid.NewGuid(), // Non-existent recipe
            RecipeInteractionEventType.Click,
            cancellationToken: CancellationToken.None);

        Assert.False(result);
        Assert.Empty(context.RecipeInteractions);
    }

    [Fact]
    public async Task LogInteractionAsync_LogsImpression_WithSource()
    {
        await using var context = CreateContext();
        var (_, recipe) = SeedUserAndRecipe(context);
        var repo = new RecipeInteractionRepository(context);
        var service = CreateService(context, repo);

        var result = await service.LogInteractionAsync(
            ClerkUserId,
            recipe.Id,
            RecipeInteractionEventType.Impression,
            source: "home_feed",
            cancellationToken: CancellationToken.None);

        Assert.True(result);
        var interaction = Assert.Single(context.RecipeInteractions);
        Assert.Equal(RecipeInteractionEventType.Impression, interaction.EventType);
        Assert.Equal(recipe.Id, interaction.RecipeId);
        Assert.Equal("home_feed", interaction.Source);
    }

    [Fact]
    public async Task LogInteractionAsync_LogsInteraction_WithAllFields()
    {
        await using var context = CreateContext();
        var (_, recipe) = SeedUserAndRecipe(context);
        var repo = new RecipeInteractionRepository(context);
        var service = CreateService(context, repo);

        var result = await service.LogInteractionAsync(
            ClerkUserId,
            recipe.Id,
            RecipeInteractionEventType.Dwell,
            source: "recipe_detail",
            sessionId: "session_123",
            dwellSeconds: 45,
            cancellationToken: CancellationToken.None);

        Assert.True(result);
        var interaction = Assert.Single(context.RecipeInteractions);
        Assert.Equal(recipe.Id, interaction.RecipeId);
        Assert.Equal(RecipeInteractionEventType.Dwell, interaction.EventType);
        Assert.Equal("recipe_detail", interaction.Source);
        Assert.Equal("session_123", interaction.SessionId);
        Assert.Equal(45, interaction.DwellSeconds);
    }

    [Fact]
    public async Task LogImpressionsAsync_ReturnsZero_WhenUserNotFound()
    {
        await using var context = CreateContext();
        var repo = new RecipeInteractionRepository(context);
        var service = CreateService(context, repo);

        var count = await service.LogImpressionsAsync(
            "nonexistent_user",
            [Guid.NewGuid(), Guid.NewGuid()],
            cancellationToken: CancellationToken.None);

        Assert.Equal(0, count);
        Assert.Empty(context.RecipeInteractions);
    }

    [Fact]
    public async Task LogImpressionsAsync_ReturnsZero_WhenEmptyList()
    {
        await using var context = CreateContext();
        SeedUserAndRecipe(context);
        var repo = new RecipeInteractionRepository(context);
        var service = CreateService(context, repo);

        var count = await service.LogImpressionsAsync(
            ClerkUserId,
            [],
            cancellationToken: CancellationToken.None);

        Assert.Equal(0, count);
        Assert.Empty(context.RecipeInteractions);
    }

    [Fact]
    public async Task LogImpressionsAsync_LogsBatchImpressions()
    {
        await using var context = CreateContext();
        var (_, recipe) = SeedUserAndRecipe(context);
        // Add more recipes for batch test
        var recipe2 = new Recipe { HouseholdId = recipe.HouseholdId, Title = "Recipe 2", Steps = "[]" };
        var recipe3 = new Recipe { HouseholdId = recipe.HouseholdId, Title = "Recipe 3", Steps = "[]" };
        context.Recipes.AddRange(recipe2, recipe3);
        await context.SaveChangesAsync();

        var repo = new RecipeInteractionRepository(context);
        var service = CreateService(context, repo);
        var recipeIds = new List<Guid> { recipe.Id, recipe2.Id, recipe3.Id };

        var count = await service.LogImpressionsAsync(
            ClerkUserId,
            recipeIds,
            source: "search_results",
            sessionId: "session_456",
            cancellationToken: CancellationToken.None);

        Assert.Equal(3, count);
        var interactions = await context.RecipeInteractions.ToListAsync();
        Assert.Equal(3, interactions.Count);
        Assert.All(interactions, i =>
        {
            Assert.Equal(RecipeInteractionEventType.Impression, i.EventType);
            Assert.Equal("search_results", i.Source);
            Assert.Equal("session_456", i.SessionId);
        });
    }

    [Fact]
    public async Task GetRecipeStatsAsync_ReturnsNull_WhenRecipeNotFound()
    {
        await using var context = CreateContext();
        var repo = new RecipeInteractionRepository(context);
        var service = CreateService(context, repo);

        var stats = await service.GetRecipeStatsAsync(Guid.NewGuid(), cancellationToken: CancellationToken.None);

        Assert.Null(stats);
    }

    [Fact]
    public async Task GetRecipeStatsAsync_ReturnsCorrectStats()
    {
        await using var context = CreateContext();
        var (user, recipe) = SeedUserAndRecipe(context);

        // Add various interactions
        context.RecipeInteractions.AddRange(
            new RecipeInteraction { UserId = user.Id, RecipeId = recipe.Id, EventType = RecipeInteractionEventType.Impression },
            new RecipeInteraction { UserId = user.Id, RecipeId = recipe.Id, EventType = RecipeInteractionEventType.Impression },
            new RecipeInteraction { UserId = user.Id, RecipeId = recipe.Id, EventType = RecipeInteractionEventType.Impression },
            new RecipeInteraction { UserId = user.Id, RecipeId = recipe.Id, EventType = RecipeInteractionEventType.Click },
            new RecipeInteraction { UserId = user.Id, RecipeId = recipe.Id, EventType = RecipeInteractionEventType.Open },
            new RecipeInteraction { UserId = user.Id, RecipeId = recipe.Id, EventType = RecipeInteractionEventType.Like },
            new RecipeInteraction { UserId = user.Id, RecipeId = recipe.Id, EventType = RecipeInteractionEventType.Save },
            new RecipeInteraction { UserId = user.Id, RecipeId = recipe.Id, EventType = RecipeInteractionEventType.Cook }
        );
        await context.SaveChangesAsync();

        var repo = new RecipeInteractionRepository(context);
        var service = CreateService(context, repo);

        var stats = await service.GetRecipeStatsAsync(recipe.Id, cancellationToken: CancellationToken.None);

        Assert.NotNull(stats);
        Assert.Equal(recipe.Id, stats.RecipeId);
        Assert.Equal(3, stats.Impressions);
        Assert.Equal(1, stats.Clicks);
        Assert.Equal(1, stats.Opens);
        Assert.Equal(1, stats.Likes);
        Assert.Equal(1, stats.Saves);
        Assert.Equal(1, stats.Cooks);
        Assert.Equal(Math.Round(1.0 / 3.0, 4), stats.ClickThroughRate);
    }

    [Fact]
    public async Task GetRecipeStatsAsync_HandlesLikeUnlikeCancellation()
    {
        await using var context = CreateContext();
        var (user, recipe) = SeedUserAndRecipe(context);

        // Like, then unlike
        context.RecipeInteractions.AddRange(
            new RecipeInteraction { UserId = user.Id, RecipeId = recipe.Id, EventType = RecipeInteractionEventType.Like },
            new RecipeInteraction { UserId = user.Id, RecipeId = recipe.Id, EventType = RecipeInteractionEventType.Unlike }
        );
        await context.SaveChangesAsync();

        var repo = new RecipeInteractionRepository(context);
        var service = CreateService(context, repo);

        var stats = await service.GetRecipeStatsAsync(recipe.Id, cancellationToken: CancellationToken.None);

        Assert.NotNull(stats);
        Assert.Equal(0, stats.Likes); // 1 like - 1 unlike = 0
    }

    [Fact]
    public async Task GetRecipeStatsAsync_ClampsNegativeValuesToZero()
    {
        await using var context = CreateContext();
        var (user, recipe) = SeedUserAndRecipe(context);

        // More unlikes/unsaves than likes/saves within the time window
        // This can happen if user liked before the time window and unliked within it
        context.RecipeInteractions.AddRange(
            new RecipeInteraction { UserId = user.Id, RecipeId = recipe.Id, EventType = RecipeInteractionEventType.Unlike },
            new RecipeInteraction { UserId = user.Id, RecipeId = recipe.Id, EventType = RecipeInteractionEventType.Unlike },
            new RecipeInteraction { UserId = user.Id, RecipeId = recipe.Id, EventType = RecipeInteractionEventType.Unsave }
        );
        await context.SaveChangesAsync();

        var repo = new RecipeInteractionRepository(context);
        var service = CreateService(context, repo);

        var stats = await service.GetRecipeStatsAsync(recipe.Id, cancellationToken: CancellationToken.None);

        Assert.NotNull(stats);
        Assert.Equal(0, stats.Likes); // 0 likes - 2 unlikes = -2, clamped to 0
        Assert.Equal(0, stats.Saves); // 0 saves - 1 unsave = -1, clamped to 0
    }

    private static RecipeInteractionService CreateService(AppDbContext context, IRecipeInteractionRepository repo)
    {
        var userRepo = new TestUserRepository(context);
        var recipeRepo = new TestRecipeRepository(context);
        return new RecipeInteractionService(repo, userRepo, recipeRepo, NullLogger<RecipeInteractionService>.Instance);
    }

    private static (User user, Recipe recipe) SeedUserAndRecipe(AppDbContext context)
    {
        var user = new User
        {
            ClerkUserId = ClerkUserId,
            Nickname = "Tester",
            Email = "test@example.com"
        };
        var household = new Household { Name = "Home", OwnerId = user.Id };
        var recipe = new Recipe
        {
            HouseholdId = household.Id,
            Title = "Test Recipe",
            Description = "Description",
            Steps = "[\"Step 1\"]"
        };

        context.Users.Add(user);
        context.Households.Add(household);
        context.Recipes.Add(recipe);
        context.SaveChanges();

        return (user, recipe);
    }

    private static TestDbContext CreateContext()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .EnableSensitiveDataLogging()
            .Options;

        var context = new TestDbContext(options, connection);
        context.Database.EnsureCreated();
        return context;
    }

    private sealed class TestUserRepository(AppDbContext context) : IUserRepository
    {
        public Task<User?> GetByClerkUserIdAsync(string clerkUserId, CancellationToken cancellationToken = default) =>
            context.Users.FirstOrDefaultAsync(u => u.ClerkUserId == clerkUserId, cancellationToken);

        public async Task<User> AddAsync(User user, CancellationToken cancellationToken = default)
        {
            context.Users.Add(user);
            await context.SaveChangesAsync(cancellationToken);
            return user;
        }

        public async Task<User> UpdateAsync(User user, CancellationToken cancellationToken = default)
        {
            context.Users.Update(user);
            await context.SaveChangesAsync(cancellationToken);
            return user;
        }
    }

    private sealed class TestRecipeRepository(AppDbContext context) : IRecipeRepository
    {
        public Task<Recipe?> GetByIdAsync(Guid recipeId, CancellationToken cancellationToken = default) =>
            context.Recipes.FindAsync([recipeId], cancellationToken).AsTask();

        public Task<Recipe?> GetDetailedByIdAsync(Guid recipeId, CancellationToken cancellationToken = default) =>
            context.Recipes.FirstOrDefaultAsync(r => r.Id == recipeId, cancellationToken);

        public Task<List<Recipe>> GetFeaturedRecipesAsync(int count, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<Recipe>());

        public Task ToggleFeaturedAsync(Guid recipeId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
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
            modelBuilder.Entity<User>()
                .Ignore(u => u.Embedding);
            modelBuilder.Entity<Household>();
            modelBuilder.Entity<Recipe>()
                .Ignore(r => r.Embedding);
            modelBuilder.Entity<RecipeInteraction>()
                .Property(ri => ri.EventType)
                .HasConversion<string>();

            modelBuilder.Ignore<HouseholdMember>();
            modelBuilder.Ignore<HouseholdInvitation>();
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
            modelBuilder.Ignore<RecipeSave>();
            modelBuilder.Ignore<RecipeComment>();
            modelBuilder.Ignore<ChecklistItem>();
            modelBuilder.Ignore<KnowledgebaseArticle>();
            modelBuilder.Ignore<UserPreference>();
            modelBuilder.Ignore<Tag>();
            modelBuilder.Ignore<TagType>();
            modelBuilder.Ignore<NameNormalizationToken>();
            modelBuilder.Ignore<NameNormalizationDictionaryVersion>();
        }

        public override void Dispose()
        {
            base.Dispose();
            _connection.Dispose();
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
