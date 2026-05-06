using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using backend.Interfaces;
using backend.Services.Vision;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace backend.Tests.Services.Vision;

public class VisionServiceTests
{
    private readonly Mock<IVisionProvider> _mockProvider;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<VisionService>> _mockLogger;
    private readonly VisionService _service;

    public VisionServiceTests()
    {
        _mockProvider = new Mock<IVisionProvider>();
        _httpClient = new HttpClient();
        _mockLogger = new Mock<ILogger<VisionService>>();
        _service = new VisionService(_mockProvider.Object, _httpClient, _mockLogger.Object);
    }

    [Fact]
    public async Task RecognizeIngredientsAsync_WithEmptyStream_ReturnsError()
    {
        // Arrange
        using var emptyStream = new MemoryStream();

        // Act
        var result = await _service.RecognizeIngredientsAsync(
            emptyStream, "test.jpg", "image/jpeg");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("No image provided.", result.ErrorMessage);
        Assert.Empty(result.Ingredients);
    }

    [Fact]
    public async Task RecognizeIngredientsAsync_WithUnsupportedMimeType_ReturnsError()
    {
        // Arrange
        using var stream = new MemoryStream(new byte[100]);

        // Act
        var result = await _service.RecognizeIngredientsAsync(
            stream, "test.pdf", "application/pdf");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Unsupported image type", result.ErrorMessage);
    }

    [Fact]
    public async Task RecognizeIngredientsAsync_WithValidImage_CallsProvider()
    {
        // Arrange
        var testData = new byte[1000];
        using var stream = new MemoryStream(testData);

        var expectedResult = new IngredientRecognitionResult(
            true,
            "ingredients",
            [
                new RecognizedIngredient("Tomato", 6, "pcs", 0.95, "Fridge", 14),
                new RecognizedIngredient("Egg", 12, "pcs", 0.92, "Fridge", 21)
            ]);

        _mockProvider
            .Setup(p => p.RecognizeIngredientsAsync(
                It.IsAny<byte[]>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _service.RecognizeIngredientsAsync(
            stream, "test.jpg", "image/jpeg");

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.Ingredients.Count);
        Assert.Equal("Tomato", result.Ingredients[0].Name);
        Assert.Equal("Egg", result.Ingredients[1].Name);

        _mockProvider.Verify(p => p.RecognizeIngredientsAsync(
            It.IsAny<byte[]>(),
            "image/jpeg",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecognizeRecipeAsync_WithValidImage_CallsProvider()
    {
        // Arrange
        var testData = new byte[1000];
        using var stream = new MemoryStream(testData);

        var expectedRecipe = new RecognizedRecipe(
            "Spaghetti Carbonara",
            "Classic Italian pasta dish",
            [
                new VisionRecipeIngredient("Spaghetti", 200, "g"),
                new VisionRecipeIngredient("Eggs", 2, "pcs"),
                new VisionRecipeIngredient("Bacon", 100, "g"),
                new VisionRecipeIngredient("Parmesan", 50, "g"),
            ],
            ["Boil pasta", "Cook bacon", "Mix eggs and cheese", "Combine all"],
            15, 20, 4, 0.88);

        var expectedResult = new RecipeRecognitionResult(true, expectedRecipe);

        _mockProvider
            .Setup(p => p.RecognizeRecipeAsync(
                It.IsAny<byte[]>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _service.RecognizeRecipeAsync(
            stream, "dish.jpg", "image/jpeg");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Recipe);
        Assert.Equal("Spaghetti Carbonara", result.Recipe.Title);
        Assert.Equal(4, result.Recipe.Ingredients.Count);
    }

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/jpg")]
    [InlineData("image/png")]
    [InlineData("image/gif")]
    [InlineData("image/webp")]
    public async Task RecognizeIngredientsAsync_SupportsAllImageTypes(string mimeType)
    {
        // Arrange
        var testData = new byte[1000];
        using var stream = new MemoryStream(testData);

        _mockProvider
            .Setup(p => p.RecognizeIngredientsAsync(
                It.IsAny<byte[]>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IngredientRecognitionResult(true, "ingredients", []));

        // Act
        _ = await _service.RecognizeIngredientsAsync(
            stream, "test.img", mimeType);

        // Assert - should reach the provider (not fail validation)
        _mockProvider.Verify(p => p.RecognizeIngredientsAsync(
            It.IsAny<byte[]>(),
            mimeType,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
