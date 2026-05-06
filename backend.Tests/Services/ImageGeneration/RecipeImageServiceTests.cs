using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using backend.Data;
using backend.Interfaces;
using backend.Models;
using backend.Services.ImageGeneration;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace backend.Tests.Services.ImageGeneration;

public class RecipeImageServiceTests
{
    [Fact]
    public async Task EnsureCoverImageUrlAsync_ReturnsExistingUrl_WhenImageAlreadyPresent()
    {
        await using var dbContext = CreateContext();
        var provider = new Mock<IImageGenerationProvider>(MockBehavior.Strict);
        var storage = new Mock<IImageStorageService>(MockBehavior.Strict);
        var service = new RecipeImageService(
            dbContext,
            provider.Object,
            storage.Object,
            NullLogger<RecipeImageService>.Instance);

        var recipe = new Recipe
        {
            Id = Guid.NewGuid(),
            HouseholdId = Guid.NewGuid(),
            Title = "Test",
            Steps = "[]",
            Visibility = RecipeVisibility.Public,
            ImageUrls = new List<string> { "https://example.com/cover.png" }
        };

        var result = await service.EnsureCoverImageUrlAsync(recipe);

        Assert.Equal("https://example.com/cover.png", result);
        provider.VerifyNoOtherCalls();
        storage.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task EnsureCoverImageUrlAsync_ReturnsNull_WhenTitleMissing()
    {
        await using var dbContext = CreateContext();
        var provider = new Mock<IImageGenerationProvider>(MockBehavior.Strict);
        var storage = new Mock<IImageStorageService>(MockBehavior.Strict);
        var service = new RecipeImageService(
            dbContext,
            provider.Object,
            storage.Object,
            NullLogger<RecipeImageService>.Instance);

        var recipe = new Recipe
        {
            Id = Guid.NewGuid(),
            HouseholdId = Guid.NewGuid(),
            Title = " ",
            Steps = "[]",
            Visibility = RecipeVisibility.Public
        };

        var result = await service.EnsureCoverImageUrlAsync(recipe);

        Assert.Null(result);
        provider.VerifyNoOtherCalls();
        storage.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task EnsureCoverImageUrlAsync_SavesGeneratedImageUrl()
    {
        await using var dbContext = CreateContext();
        var provider = new Mock<IImageGenerationProvider>();
        var storage = new Mock<IImageStorageService>();
        var imageBytes = new byte[] { 1, 2, 3 };
        var imageUrl = "https://cdn.example.com/recipe.png";

        provider
            .Setup(p => p.GenerateImageAsync(It.IsAny<ImageGenerationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImageGenerationResult(true, imageBytes, "image/png"));
        storage
            .Setup(s => s.UploadAsync(It.IsAny<IFormFile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(imageUrl);

        var service = new RecipeImageService(
            dbContext,
            provider.Object,
            storage.Object,
            NullLogger<RecipeImageService>.Instance);

        var recipe = new Recipe
        {
            Id = Guid.NewGuid(),
            HouseholdId = Guid.NewGuid(),
            Title = "Test",
            Description = "Rich and savory.",
            Steps = "[]",
            Visibility = RecipeVisibility.Public
        };

        dbContext.Recipes.Add(recipe);
        await dbContext.SaveChangesAsync();

        var result = await service.EnsureCoverImageUrlAsync(recipe);

        Assert.Equal(imageUrl, result);

        var saved = await dbContext.Recipes.FindAsync(recipe.Id);
        Assert.NotNull(saved);
        Assert.Single(saved!.ImageUrls!);
        Assert.Equal(imageUrl, saved.ImageUrls![0]);
        provider.Verify(p => p.GenerateImageAsync(It.IsAny<ImageGenerationRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        storage.Verify(s => s.UploadAsync(It.IsAny<IFormFile>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnsureCoverImageUrlAsync_TruncatesDescriptionAtWordBoundary()
    {
        await using var dbContext = CreateContext();
        var provider = new Mock<IImageGenerationProvider>();
        var storage = new Mock<IImageStorageService>(MockBehavior.Strict);
        string? capturedPrompt = null;

        provider
            .Setup(p => p.GenerateImageAsync(It.IsAny<ImageGenerationRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ImageGenerationRequest, CancellationToken>((request, _) => capturedPrompt = request.Prompt)
            .ReturnsAsync(new ImageGenerationResult(false, null, null, "fail"));

        var service = new RecipeImageService(
            dbContext,
            provider.Object,
            storage.Object,
            NullLogger<RecipeImageService>.Instance);

        var words = Enumerable.Repeat("spicy", 40);
        var description = string.Join(" ", words);
        var expectedWords = new List<string>();
        var currentLength = 0;
        foreach (var word in words)
        {
            var addLength = currentLength == 0 ? word.Length : word.Length + 1;
            if (currentLength + addLength > 200)
            {
                break;
            }

            expectedWords.Add(word);
            currentLength += addLength;
        }

        var expectedDescription = string.Join(" ", expectedWords);
        var recipe = new Recipe
        {
            Id = Guid.NewGuid(),
            HouseholdId = Guid.NewGuid(),
            Title = "Spicy Chili",
            Description = description,
            Steps = "[]",
            Visibility = RecipeVisibility.Public
        };

        var result = await service.EnsureCoverImageUrlAsync(recipe);

        Assert.Null(result);
        Assert.NotNull(capturedPrompt);
        Assert.Contains($". {expectedDescription}.", capturedPrompt!);
        Assert.DoesNotContain($"{expectedDescription} sp", capturedPrompt!);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"RecipeImageServiceTests_{Guid.NewGuid():N}")
            .Options;

        return new TestAppDbContext(options);
    }

    private sealed class TestAppDbContext : AppDbContext
    {
        public TestAppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<User>().Ignore(u => u.Embedding);
            modelBuilder.Entity<Ingredient>().Ignore(i => i.Embedding);
            modelBuilder.Entity<Recipe>().Ignore(r => r.Embedding);
        }
    }
}
