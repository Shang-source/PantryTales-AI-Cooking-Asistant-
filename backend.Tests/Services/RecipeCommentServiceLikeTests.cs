using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using backend.Data;
using backend.Interfaces;
using backend.Models;
using backend.Repository;
using backend.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using RecipeIngredientEntity = backend.Models.RecipeIngredient;
using RecipeIngredientTagEntity = backend.Models.RecipeIngredientTag;

namespace backend.Tests.Services;

public class RecipeCommentServiceLikeTests
{
    [Fact]
    public async Task ToggleLikeAsync_TogglesLikeState_ForSameUserAndComment()
    {
        await using var context = CreateContext();

        var user = new User
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            ClerkUserId = "clerk_test",
            Email = "test@example.com",
            Nickname = "Test",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var recipe = new Recipe
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000010"),
            Type = RecipeType.User,
            Visibility = RecipeVisibility.Public,
            Title = "Test recipe",
            Description = "Test recipe",
            Steps = "[]",
            AuthorId = user.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CommentsCount = 0,
            LikesCount = 0,
            SavedCount = 0,
            Difficulty = RecipeDifficulty.None
        };
        var comment = new RecipeComment
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000100"),
            RecipeId = recipe.Id,
            UserId = user.Id,
            Content = "Hello",
            CreatedAt = DateTime.UtcNow
        };

        context.Users.Add(user);
        context.Recipes.Add(recipe);
        context.RecipeComments.Add(comment);
        await context.SaveChangesAsync();

        var commentRepository = new RecipeCommentRepository(context, NullLogger<RecipeCommentRepository>.Instance);
        var service = new RecipeCommentService(
            commentRepository,
            new FakeUserRepository(user),
            new FakeRecipeRepository(recipe),
            NullLogger<RecipeCommentService>.Instance);

        var liked = await service.ToggleLikeAsync(comment.Id, user.ClerkUserId, CancellationToken.None);
        Assert.Equal(ToggleLikeResultStatus.Success, liked.Status);
        Assert.NotNull(liked.Data);
        Assert.True(liked.Data!.IsLiked);
        Assert.Equal(1, liked.Data.LikeCount);

        var unliked = await service.ToggleLikeAsync(comment.Id, user.ClerkUserId, CancellationToken.None);
        Assert.Equal(ToggleLikeResultStatus.Success, unliked.Status);
        Assert.NotNull(unliked.Data);
        Assert.False(unliked.Data!.IsLiked);
        Assert.Equal(0, unliked.Data.LikeCount);
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
            modelBuilder.Entity<Recipe>()
                .Ignore(r => r.Embedding);
            modelBuilder.Entity<RecipeComment>();
            modelBuilder.Entity<CommentLike>();

            modelBuilder.Ignore<Tag>();
            modelBuilder.Ignore<TagType>();
            modelBuilder.Ignore<Ingredient>();
            modelBuilder.Ignore<IngredientAlias>();
            modelBuilder.Ignore<IngredientUnit>();
            modelBuilder.Ignore<IngredientTag>();
            modelBuilder.Ignore<InventoryItem>();
            modelBuilder.Ignore<InventoryItemTag>();
            modelBuilder.Ignore<RecipeIngredientEntity>();
            modelBuilder.Ignore<RecipeIngredientTagEntity>();
            modelBuilder.Ignore<RecipeTag>();
            modelBuilder.Ignore<RecipeLike>();
            modelBuilder.Ignore<RecipeSave>();
            modelBuilder.Ignore<RecipeInteraction>();
            modelBuilder.Ignore<ChecklistItem>();
            modelBuilder.Ignore<KnowledgebaseArticle>();
            modelBuilder.Ignore<Household>();
            modelBuilder.Ignore<HouseholdMember>();
            modelBuilder.Ignore<HouseholdInvitation>();
            modelBuilder.Ignore<UserPreference>();
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

    private sealed class FakeUserRepository(User user) : IUserRepository
    {
        public Task<User?> GetByClerkUserIdAsync(string clerkUserId, CancellationToken cancellationToken = default)
            => Task.FromResult(clerkUserId == user.ClerkUserId ? user : null);

        public Task<User> AddAsync(User user, CancellationToken cancellationToken = default)
            => Task.FromResult(user);

        public Task<User> UpdateAsync(User user, CancellationToken cancellationToken = default)
            => Task.FromResult(user);
    }

    private sealed class FakeRecipeRepository(Recipe recipe) : IRecipeRepository
    {
        public Task<Recipe?> GetByIdAsync(Guid recipeId, CancellationToken cancellationToken = default)
            => Task.FromResult(recipeId == recipe.Id ? recipe : null);

        public Task<Recipe?> GetDetailedByIdAsync(Guid recipeId, CancellationToken cancellationToken = default)
            => Task.FromResult(recipeId == recipe.Id ? recipe : null);

        public Task<List<Recipe>> GetFeaturedRecipesAsync(int count, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<Recipe>());

        public Task ToggleFeaturedAsync(Guid recipeId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}

