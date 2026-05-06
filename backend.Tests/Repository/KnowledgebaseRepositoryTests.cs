using System;
using System.Linq;
using System.Threading.Tasks;
using backend.Data;
using backend.Models;
using backend.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace backend.Tests.Repository;

/// <summary>
/// Integration tests for KnowledgebaseRepository using EF Core InMemory database.
/// Tests CRUD operations for admin article management.
/// </summary>
public class KnowledgebaseRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly KnowledgebaseRepository _repository;
    private readonly Tag _testTag;

    public KnowledgebaseRepositoryTests()
    {
        _context = CreateInMemoryContext();
        _repository = new KnowledgebaseRepository(_context);

        // Create a test tag for articles
        _testTag = new Tag
        {
            Id = 1,
            Name = "test-tag",
            DisplayName = "Test Tag",
            Type = "general",
            IsActive = true
        };
        _context.Tags.Add(_testTag);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    #region GetAllArticlesAsync Tests

    [Fact]
    public async Task GetAllArticlesAsync_ReturnsEmptyList_WhenNoArticles()
    {
        var result = await _repository.GetAllArticlesAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllArticlesAsync_ReturnsAllArticles_OrderedByUpdatedAtDesc()
    {
        // Arrange
        var article1 = CreateTestArticle("First Article");
        article1.UpdatedAt = DateTime.UtcNow.AddDays(-2);

        var article2 = CreateTestArticle("Second Article");
        article2.UpdatedAt = DateTime.UtcNow.AddDays(-1);

        var article3 = CreateTestArticle("Third Article");
        article3.UpdatedAt = DateTime.UtcNow;

        _context.KnowledgebaseArticles.AddRange(article1, article2, article3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllArticlesAsync();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("Third Article", result[0].Title);
        Assert.Equal("Second Article", result[1].Title);
        Assert.Equal("First Article", result[2].Title);
    }

    [Fact]
    public async Task GetAllArticlesAsync_IncludesTag()
    {
        // Arrange
        var article = CreateTestArticle("Article With Tag");
        _context.KnowledgebaseArticles.Add(article);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllArticlesAsync();

        // Assert
        Assert.Single(result);
        Assert.NotNull(result[0].Tag);
        Assert.Equal(_testTag.Id, result[0].Tag.Id);
    }

    [Fact]
    public async Task GetAllArticlesAsync_FiltersByTagId_WhenTagIdProvided()
    {
        // Arrange
        var secondTag = new Tag
        {
            Id = 2,
            Name = "second-tag",
            DisplayName = "Second Tag",
            Type = "general",
            IsActive = true
        };
        _context.Tags.Add(secondTag);
        await _context.SaveChangesAsync();

        var article1 = CreateTestArticle("First Article");
        var article2 = CreateTestArticle("Second Article");
        article2.TagId = secondTag.Id;

        _context.KnowledgebaseArticles.AddRange(article1, article2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllArticlesAsync(tagId: _testTag.Id);

        // Assert
        Assert.Single(result);
        Assert.Equal("First Article", result[0].Title);
    }

    [Fact]
    public async Task GetAllArticlesAsync_IncludesPublishedAndUnpublished()
    {
        // Arrange
        var publishedArticle = CreateTestArticle("Published");
        publishedArticle.IsPublished = true;

        var draftArticle = CreateTestArticle("Draft");
        draftArticle.IsPublished = false;

        _context.KnowledgebaseArticles.AddRange(publishedArticle, draftArticle);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllArticlesAsync();

        // Assert
        Assert.Equal(2, result.Count);
    }

    #region ListTagsForArticlesAsync Tests

    [Fact]
    public async Task ListTagsForArticlesAsync_ReturnsTagsInPriorityOrder()
    {
        // Arrange
        var recipeTag = new Tag { Id = 10, Name = "recipe-tag", DisplayName = "My Recipe", IsActive = true };
        var foodTag = new Tag { Id = 11, Name = "food-tag", DisplayName = "Good Food", IsActive = true };
        var scienceTag = new Tag { Id = 12, Name = "science-tag", DisplayName = "Food Science", IsActive = true };
        var normalTag = new Tag { Id = 13, Name = "other-tag", DisplayName = "General Stat", IsActive = true };

        _context.Tags.AddRange(recipeTag, foodTag, scienceTag, normalTag);
        
        // Ensure they have published articles so they appear in the list
        _context.KnowledgebaseArticles.AddRange(
            new KnowledgebaseArticle { Id = Guid.NewGuid(), TagId = recipeTag.Id, Title = "A", IsPublished = true },
            new KnowledgebaseArticle { Id = Guid.NewGuid(), TagId = foodTag.Id, Title = "B", IsPublished = true },
            new KnowledgebaseArticle { Id = Guid.NewGuid(), TagId = scienceTag.Id, Title = "C", IsPublished = true },
            new KnowledgebaseArticle { Id = Guid.NewGuid(), TagId = normalTag.Id, Title = "D", IsPublished = true }
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.ListTagsForArticlesAsync();

        // Assert
        Assert.Equal(4, result.Count);
        // Priority: Recipe (1) > Food (3) > General (50) > Science (99)
        Assert.Equal(recipeTag.Id, result[0].Id);
        Assert.Equal(foodTag.Id, result[1].Id);
        Assert.Equal(normalTag.Id, result[2].Id);
        Assert.Equal(scienceTag.Id, result[3].Id);
    }

    #endregion

    #region ListPublishedByTagAsync Tests

    [Fact]
    public async Task ListPublishedByTagAsync_ReturnsPagedResults()
    {
        // Arrange
        for (int i = 1; i <= 5; i++)
        {
            var article = CreateTestArticle($"Page Test {i}");
            // Ensure strict order by UpdatedAt
            article.UpdatedAt = DateTime.UtcNow.AddMinutes(i); 
            _context.KnowledgebaseArticles.Add(article);
        }
        await _context.SaveChangesAsync();

        // Act - Page 1, Size 2 (Should get item 5 and 4 because of Descending sort)
        var (items, totalCount) = await _repository.ListPublishedByTagAsync(_testTag.Id, 1, 2);

        // Assert
        Assert.Equal(5, totalCount);
        Assert.Equal(2, items.Count);
        Assert.Equal("Page Test 5", items[0].Title);
        Assert.Equal("Page Test 4", items[1].Title);
    }

    [Fact]
    public async Task ListPublishedByTagAsync_ReturnsCorrectSecondPage()
    {
        // Arrange
        for (int i = 1; i <= 5; i++)
        {
            var article = CreateTestArticle($"Page Test {i}");
            article.UpdatedAt = DateTime.UtcNow.AddMinutes(i);
            _context.KnowledgebaseArticles.Add(article);
        }
        await _context.SaveChangesAsync();

        // Act - Page 2, Size 2 (Should get item 3 and 2)
        var (items, totalCount) = await _repository.ListPublishedByTagAsync(_testTag.Id, 2, 2);

        // Assert
        Assert.Equal(5, totalCount);
        Assert.Equal(2, items.Count);
        Assert.Equal("Page Test 3", items[0].Title);
        Assert.Equal("Page Test 2", items[1].Title);
    }

    #endregion

    #endregion

    #region GetArticleByIdForEditAsync Tests

    [Fact]
    public async Task GetArticleByIdForEditAsync_ReturnsNull_WhenArticleNotFound()
    {
        var result = await _repository.GetArticleByIdForEditAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetArticleByIdForEditAsync_ReturnsArticle_WhenFound()
    {
        // Arrange
        var article = CreateTestArticle("Test Article");
        _context.KnowledgebaseArticles.Add(article);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetArticleByIdForEditAsync(article.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(article.Id, result.Id);
        Assert.Equal("Test Article", result.Title);
    }

    [Fact]
    public async Task GetArticleByIdForEditAsync_ReturnsUnpublishedArticles()
    {
        // Arrange - The public GetArticleByIdAsync requires IsPublished, but this one shouldn't
        var draftArticle = CreateTestArticle("Draft Article");
        draftArticle.IsPublished = false;
        _context.KnowledgebaseArticles.Add(draftArticle);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetArticleByIdForEditAsync(draftArticle.Id);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsPublished);
    }

    [Fact]
    public async Task GetArticleByIdForEditAsync_ReturnsTrackedEntity()
    {
        // Arrange
        var article = CreateTestArticle("Tracked Article");
        _context.KnowledgebaseArticles.Add(article);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetArticleByIdForEditAsync(article.Id);
        result!.Title = "Modified Title";
        await _context.SaveChangesAsync();

        // Assert - changes should be saved due to tracking
        var reloaded = await _context.KnowledgebaseArticles.FindAsync(article.Id);
        Assert.Equal("Modified Title", reloaded!.Title);
    }

    #endregion

    #region UpdateArticleAsync Tests

    [Fact]
    public async Task UpdateArticleAsync_SavesChanges()
    {
        // Arrange
        var article = CreateTestArticle("Original Title");
        _context.KnowledgebaseArticles.Add(article);
        await _context.SaveChangesAsync();

        // Detach to simulate real scenario
        _context.Entry(article).State = EntityState.Detached;

        article.Title = "Updated Title";
        article.Content = "Updated Content";

        // Act
        await _repository.UpdateArticleAsync(article);

        // Assert
        var updated = await _context.KnowledgebaseArticles.FindAsync(article.Id);
        Assert.Equal("Updated Title", updated!.Title);
        Assert.Equal("Updated Content", updated.Content);
    }

    #endregion

    #region DeleteArticleAsync Tests

    [Fact]
    public async Task DeleteArticleAsync_RemovesArticle()
    {
        // Arrange
        var article = CreateTestArticle("To Be Deleted");
        _context.KnowledgebaseArticles.Add(article);
        await _context.SaveChangesAsync();
        var articleId = article.Id;

        // Act
        await _repository.DeleteArticleAsync(article);

        // Assert
        var deleted = await _context.KnowledgebaseArticles.FindAsync(articleId);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task DeleteArticleAsync_DoesNotAffectOtherArticles()
    {
        // Arrange
        var article1 = CreateTestArticle("Keep This");
        var article2 = CreateTestArticle("Delete This");
        _context.KnowledgebaseArticles.AddRange(article1, article2);
        await _context.SaveChangesAsync();

        // Act
        await _repository.DeleteArticleAsync(article2);

        // Assert
        var remaining = await _context.KnowledgebaseArticles.ToListAsync();
        Assert.Single(remaining);
        Assert.Equal("Keep This", remaining[0].Title);
    }

    #endregion

    #region CreateArticleAsync Tests

    [Fact]
    public async Task CreateArticleAsync_PersistsArticle()
    {
        // Arrange
        var article = CreateTestArticle("New Article");

        // Act
        var result = await _repository.CreateArticleAsync(article);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.Id);

        var persisted = await _context.KnowledgebaseArticles.FindAsync(result.Id);
        Assert.NotNull(persisted);
        Assert.Equal("New Article", persisted.Title);
    }

    #endregion

    private static AppDbContext CreateInMemoryContext()
    {
        var dbName = "TestDb_" + Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new TestAppDbContext(options);
    }

    private KnowledgebaseArticle CreateTestArticle(string title) => new()
    {
        Id = Guid.NewGuid(),
        TagId = _testTag.Id,
        Title = title,
        Content = $"Content for {title}",
        IsPublished = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    /// <summary>
    /// Test DbContext that skips PostgreSQL-specific vector extensions.
    /// </summary>
    private sealed class TestAppDbContext : AppDbContext
    {
        public TestAppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // Ignore vector embedding properties that require PostgreSQL
            modelBuilder.Entity<User>().Ignore(u => u.Embedding);
            modelBuilder.Entity<Ingredient>().Ignore(i => i.Embedding);
            modelBuilder.Entity<Recipe>().Ignore(r => r.Embedding);
        }
    }
}
