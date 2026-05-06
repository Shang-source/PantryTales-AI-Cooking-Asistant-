using System.IO;
using System.Threading;
using System.Threading.Tasks;
using backend.Controllers;
using backend.Dtos;
using backend.Dtos.Vision;
using backend.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace backend.Tests.Controllers;

public class VisionControllerTests
{
    private readonly Mock<IVisionService> _mockService;
    private readonly Mock<ILogger<VisionController>> _mockLogger;
    private readonly VisionController _controller;

    public VisionControllerTests()
    {
        _mockService = new Mock<IVisionService>();
        _mockLogger = new Mock<ILogger<VisionController>>();
        _controller = new VisionController(_mockService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task RecognizeIngredients_WithNoImage_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.RecognizeIngredients(null!, CancellationToken.None);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(badRequest.Value);
        Assert.Equal(400, response.Code);
        Assert.Equal("No image provided.", response.Message);
    }

    [Fact]
    public async Task RecognizeIngredients_WithValidImage_ReturnsSuccess()
    {
        // Arrange
        var mockFile = CreateMockFormFile("test.jpg", "image/jpeg", 1000);

        var serviceResult = new IngredientRecognitionResult(
            true,
            "ingredients",
            [
                new RecognizedIngredient("Tomato", 6, "pcs", 0.95, "Fridge", 14)
            ]);

        _mockService
            .Setup(s => s.RecognizeIngredientsAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        // Act
        var result = await _controller.RecognizeIngredients(mockFile, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<IngredientRecognitionResponseDto>>(okResult.Value);
        Assert.Equal(0, response.Code);
        Assert.NotNull(response.Data);
        Assert.Single(response.Data.Ingredients);
        Assert.Equal("Tomato", response.Data.Ingredients[0].Name);
    }

    [Fact]
    public async Task RecognizeIngredients_WithServiceError_ReturnsBadRequest()
    {
        // Arrange
        var mockFile = CreateMockFormFile("test.jpg", "image/jpeg", 1000);

        var serviceResult = new IngredientRecognitionResult(
            false, "unknown", [], ErrorMessage: "API error occurred");

        _mockService
            .Setup(s => s.RecognizeIngredientsAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        // Act
        var result = await _controller.RecognizeIngredients(mockFile, CancellationToken.None);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<IngredientRecognitionResponseDto>>(badRequest.Value);
        Assert.Equal(400, response.Code);
        Assert.Equal("API error occurred", response.Message);
    }

    [Fact]
    public async Task RecognizeRecipe_WithValidImage_ReturnsSuccess()
    {
        // Arrange
        var mockFile = CreateMockFormFile("dish.jpg", "image/jpeg", 1000);

        var recipe = new RecognizedRecipe(
            "Pasta Carbonara",
            "Italian classic",
            [
                new VisionRecipeIngredient("Pasta", 200, "g"),
                new VisionRecipeIngredient("Eggs", 2, "pcs"),
                new VisionRecipeIngredient("Bacon", 100, "g"),
            ],
            ["Boil pasta", "Cook bacon", "Combine"],
            15, 20, 4, 0.88);

        var serviceResult = new RecipeRecognitionResult(true, recipe);

        _mockService
            .Setup(s => s.RecognizeRecipeAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        // Act
        var result = await _controller.RecognizeRecipe(mockFile, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<RecipeRecognitionResponseDto>>(okResult.Value);
        Assert.Equal(0, response.Code);
        Assert.NotNull(response.Data?.Recipe);
        Assert.Equal("Pasta Carbonara", response.Data.Recipe.Title);
    }

    private static IFormFile CreateMockFormFile(string fileName, string contentType, int length)
    {
        var content = new byte[length];

        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.FileName).Returns(fileName);
        mockFile.Setup(f => f.ContentType).Returns(contentType);
        mockFile.Setup(f => f.Length).Returns(length);
        mockFile.Setup(f => f.OpenReadStream()).Returns(() => new MemoryStream(content));

        return mockFile.Object;
    }
}
