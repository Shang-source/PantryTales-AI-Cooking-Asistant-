using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using backend.Data;
using backend.Interfaces;
using backend.Models;
using backend.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace backend.Tests.Services;

public class NameNormalizationServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestDbContext _context;
    private readonly TestNormalizationRepository _repository;
    private readonly MemoryCache _cache;
    private readonly NameNormalizationService _service;

    public NameNormalizationServiceTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new TestDbContext(options, _connection);
        _context.Database.EnsureCreated();

        _repository = new TestNormalizationRepository(_context);
        _cache = new MemoryCache(new MemoryCacheOptions());
        _service = new NameNormalizationService(
            _context,
            _repository,
            _cache,
            NullLogger<NameNormalizationService>.Instance);
    }

    public void Dispose()
    {
        _cache.Dispose();
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    #region NormalizeAsync Tests

    [Fact]
    public async Task NormalizeAsync_ReturnsEmptyString_WhenInputIsNullOrWhitespace()
    {
        var (normalized, removed) = await _service.NormalizeAsync("");
        Assert.Equal(string.Empty, normalized);
        Assert.Empty(removed);

        (normalized, removed) = await _service.NormalizeAsync("   ");
        Assert.Equal(string.Empty, normalized);
        Assert.Empty(removed);
    }

    [Fact]
    public async Task NormalizeAsync_ReturnsOriginal_WhenNoTokensMatch()
    {
        await SeedTokensAsync(new[] { ("brand", NameNormalizationTokenCategory.Brand, false) });

        var (normalized, removed) = await _service.NormalizeAsync("Fresh Organic Milk");

        Assert.Equal("Fresh Organic Milk", normalized);
        Assert.Empty(removed);
    }

    [Fact]
    public async Task NormalizeAsync_RemovesMatchingToken_CaseInsensitive()
    {
        await SeedTokensAsync(new[] { ("countdown", NameNormalizationTokenCategory.Brand, false) });

        var (normalized, removed) = await _service.NormalizeAsync("Countdown Fresh Milk");

        Assert.Equal("Fresh Milk", normalized);
        Assert.Single(removed);
        Assert.Contains("Countdown", removed);
    }

    [Fact]
    public async Task NormalizeAsync_RemovesMultipleTokens()
    {
        await SeedTokensAsync(new[]
        {
            ("countdown", NameNormalizationTokenCategory.Brand, false),
            ("500ml", NameNormalizationTokenCategory.Unit, false),
            ("special", NameNormalizationTokenCategory.Promo, false)
        });

        var (normalized, removed) = await _service.NormalizeAsync("Countdown Special Fresh Milk 500ml");

        Assert.Equal("Fresh Milk", normalized);
        Assert.Equal(3, removed.Count);
    }

    [Fact]
    public async Task NormalizeAsync_HandlesRegexPatterns()
    {
        // Regex pattern to match any size like "100g", "500ml", "1kg"
        await SeedTokensAsync(new[] { (@"\d+\s*(g|ml|kg|l)\b", NameNormalizationTokenCategory.Unit, true) });

        var (normalized, removed) = await _service.NormalizeAsync("Butter 250g Unsalted");

        Assert.Equal("Butter Unsalted", normalized);
        Assert.Single(removed);
        Assert.Contains("250g", removed);
    }

    [Fact]
    public async Task NormalizeAsync_HandlesInvalidRegexGracefully()
    {
        // Invalid regex pattern (unbalanced parenthesis)
        await SeedTokensAsync(new[] { (@"(invalid[", NameNormalizationTokenCategory.Noise, true) });

        var (normalized, removed) = await _service.NormalizeAsync("Test Product");

        // Should not throw, just skip the invalid pattern
        Assert.Equal("Test Product", normalized);
        Assert.Empty(removed);
    }

    [Fact]
    public async Task NormalizeAsync_CollapsesMultipleSpaces()
    {
        await SeedTokensAsync(new[] { ("organic", NameNormalizationTokenCategory.Noise, false) });

        var (normalized, removed) = await _service.NormalizeAsync("Fresh   Organic   Apples");

        Assert.Equal("Fresh Apples", normalized);
        Assert.DoesNotContain("  ", normalized);
    }

    [Fact]
    public async Task NormalizeAsync_DeduplicatesRemovedTokens_CaseInsensitive()
    {
        await SeedTokensAsync(new[] { ("sale", NameNormalizationTokenCategory.Promo, false) });

        var (normalized, removed) = await _service.NormalizeAsync("SALE Fresh Milk sale");

        Assert.Equal("Fresh Milk", normalized);
        // Should only contain one entry despite different cases
        Assert.Single(removed);
    }

    [Fact]
    public async Task NormalizeAsync_OnlyRemovesActiveTokens()
    {
        var token = new NameNormalizationToken
        {
            Token = "inactive",
            Category = NameNormalizationTokenCategory.Brand,
            IsActive = false
        };
        _context.NameNormalizationTokens.Add(token);
        await _context.SaveChangesAsync();
        _cache.Remove("normalization_tokens");

        var (normalized, removed) = await _service.NormalizeAsync("Inactive Brand Milk");

        Assert.Equal("Inactive Brand Milk", normalized);
        Assert.Empty(removed);
    }

    #endregion

    #region ProcessInventoryItemBatchAsync Tests

    [Fact]
    public async Task ProcessInventoryItemBatchAsync_ReturnsZero_WhenNoItemsNeedProcessing()
    {
        var result = await _service.ProcessInventoryItemBatchAsync(10);
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task ProcessInventoryItemBatchAsync_NormalizesItemsWithNullNormalizedName()
    {
        await SeedTokensAsync(new[] { ("countdown", NameNormalizationTokenCategory.Brand, false) });

        var household = new Household { Name = "Test" };
        _context.Households.Add(household);

        var item = new InventoryItem
        {
            HouseholdId = household.Id,
            Name = "Countdown Fresh Milk",
            NormalizedName = null,
            Unit = "bottle",
            ResolveStatus = IngredientResolveStatus.Resolved // Already resolved, skip ingredient resolution
        };
        _context.InventoryItems.Add(item);
        await _context.SaveChangesAsync();

        var result = await _service.ProcessInventoryItemBatchAsync(10);

        Assert.Equal(1, result);
        Assert.Equal("Fresh Milk", item.NormalizedName);
        Assert.NotNull(item.NameNormalizationDictionaryVersion);
        Assert.NotNull(item.NameNormalizationAlgorithmVersion);
    }

    [Fact]
    public async Task ProcessInventoryItemBatchAsync_RespectsBatchSize()
    {
        var household = new Household { Name = "Test" };
        _context.Households.Add(household);

        for (var i = 0; i < 5; i++)
        {
            _context.InventoryItems.Add(new InventoryItem
            {
                HouseholdId = household.Id,
                Name = $"Product {i}",
                NormalizedName = null,
                Unit = "piece",
                ResolveStatus = IngredientResolveStatus.Resolved // Already resolved, skip ingredient resolution
            });
        }
        await _context.SaveChangesAsync();

        var result = await _service.ProcessInventoryItemBatchAsync(2);

        Assert.Equal(2, result);
    }

    #endregion

    #region Helpers

    private async Task SeedTokensAsync(IEnumerable<(string token, NameNormalizationTokenCategory category, bool isRegex)> tokens)
    {
        foreach (var (token, category, isRegex) in tokens)
        {
            _context.NameNormalizationTokens.Add(new NameNormalizationToken
            {
                Token = token,
                Category = category,
                IsRegex = isRegex,
                IsActive = true
            });
        }
        await _context.SaveChangesAsync();
        _cache.Remove("normalization_tokens");
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
            modelBuilder.Entity<Household>();
            modelBuilder.Entity<InventoryItem>()
                .Property(i => i.ResolveStatus)
                .HasConversion<string>();
            modelBuilder.Entity<Ingredient>()
                .Ignore(i => i.Embedding);
            modelBuilder.Entity<IngredientAlias>()
                .Property(a => a.Status)
                .HasConversion<string>();
            modelBuilder.Entity<NameNormalizationToken>()
                .Property(t => t.Category)
                .HasConversion<string>();
            modelBuilder.Entity<NameNormalizationDictionaryVersion>();

            // Ignore entities not needed for these tests
            modelBuilder.Ignore<User>();
            modelBuilder.Ignore<Recipe>();
            modelBuilder.Ignore<backend.Models.RecipeIngredient>();
            modelBuilder.Ignore<RecipeTag>();
            modelBuilder.Ignore<RecipeIngredientTag>();
            modelBuilder.Ignore<RecipeLike>();
            modelBuilder.Ignore<RecipeSave>();
            modelBuilder.Ignore<RecipeComment>();
            modelBuilder.Ignore<RecipeInteraction>();
            modelBuilder.Ignore<ChecklistItem>();
            modelBuilder.Ignore<HouseholdMember>();
            modelBuilder.Ignore<HouseholdInvitation>();
            modelBuilder.Ignore<UserPreference>();
            modelBuilder.Ignore<Tag>();
            modelBuilder.Ignore<TagType>();
            modelBuilder.Ignore<IngredientTag>();
            modelBuilder.Ignore<KnowledgebaseArticle>();
            modelBuilder.Ignore<InventoryItemTag>();
            modelBuilder.Ignore<IngredientUnit>();
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

    private sealed class TestNormalizationRepository : INameNormalizationRepository
    {
        private readonly AppDbContext _context;
        private NameNormalizationDictionaryVersion? _version;

        public TestNormalizationRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<NameNormalizationToken>> GetTokensAsync(
            NameNormalizationTokenCategory? category = null,
            bool? isActive = null,
            CancellationToken cancellationToken = default)
        {
            var query = _context.NameNormalizationTokens.AsQueryable();
            if (category.HasValue) query = query.Where(t => t.Category == category.Value);
            if (isActive.HasValue) query = query.Where(t => t.IsActive == isActive.Value);
            return await query.ToListAsync(cancellationToken);
        }

        public Task<NameNormalizationToken?> GetTokenByIdAsync(long id, CancellationToken cancellationToken = default) =>
            _context.NameNormalizationTokens.FindAsync(new object[] { id }, cancellationToken).AsTask();

        public Task<bool> TokenExistsAsync(string token, CancellationToken cancellationToken = default) =>
            _context.NameNormalizationTokens.AnyAsync(t => t.Token.ToLower() == token.ToLower(), cancellationToken);

        public async Task<NameNormalizationToken> AddTokenAsync(NameNormalizationToken token, CancellationToken cancellationToken = default)
        {
            _context.NameNormalizationTokens.Add(token);
            await _context.SaveChangesAsync(cancellationToken);
            return token;
        }

        public async Task UpdateTokenAsync(NameNormalizationToken token, CancellationToken cancellationToken = default)
        {
            _context.NameNormalizationTokens.Update(token);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteTokenAsync(NameNormalizationToken token, CancellationToken cancellationToken = default)
        {
            _context.NameNormalizationTokens.Remove(token);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public Task<(int Created, int Skipped)> AddTokensAsync(IEnumerable<NameNormalizationToken> tokens, CancellationToken cancellationToken = default) =>
            Task.FromResult((0, 0));

        public Task<NameNormalizationDictionaryVersion> GetVersionAsync(CancellationToken cancellationToken = default)
        {
            _version ??= new NameNormalizationDictionaryVersion { Id = 1, DictionaryVersion = 1, AlgorithmVersion = 1 };
            return Task.FromResult(_version);
        }

        public Task IncrementDictionaryVersionAsync(CancellationToken cancellationToken = default)
        {
            if (_version != null) _version.DictionaryVersion++;
            return Task.CompletedTask;
        }
    }

    #endregion
}
