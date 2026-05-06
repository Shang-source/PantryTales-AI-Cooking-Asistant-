using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using backend.Controllers;
using backend.Dtos;
using backend.Dtos.Recipes;
using backend.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace backend.Tests.Controllers;

public class NutritionControllerTests
{
    #region CalculateNutrition Tests

    [Fact]
    public async Task CalculateNutrition_ReturnsBadRequest_WhenNoIngredients()
    {
        var fakeService = new FakeNutritionService();
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));
        var request = new CalculateNutritionRequestDto([], 1);

        var result = await controller.CalculateNutrition(request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<NutritionResponseDto>>(badRequest.Value);
        Assert.Equal(400, response.Code);
        Assert.Equal("No ingredients provided.", response.Message);
    }

    [Fact]
    public async Task CalculateNutrition_ReturnsBadRequest_WhenIngredientsNull()
    {
        var fakeService = new FakeNutritionService();
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));
        var request = new CalculateNutritionRequestDto(null!, 1);

        var result = await controller.CalculateNutrition(request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<NutritionResponseDto>>(badRequest.Value);
        Assert.Equal(400, response.Code);
        Assert.Equal("No ingredients provided.", response.Message);
    }

    [Fact]
    public async Task CalculateNutrition_ReturnsBadRequest_WhenCalculationFails()
    {
        var failedResult = new NutritionResponseDto(false, null, [], "Some error occurred");
        var fakeService = new FakeNutritionService { CalculationResult = failedResult };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));
        var request = NewValidRequest();

        var result = await controller.CalculateNutrition(request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<NutritionResponseDto>>(badRequest.Value);
        Assert.Equal(400, response.Code);
        Assert.Equal("Some error occurred", response.Message);
    }

    [Fact]
    public async Task CalculateNutrition_ReturnsOk_WhenCalculationSucceeds()
    {
        var nutritionData = new NutritionDataDto(200, 10, 5, 30, 2, 100, 1, 3, 0, 4, 50, 2);
        var successResult = new NutritionResponseDto(true, nutritionData, []);
        var fakeService = new FakeNutritionService { CalculationResult = successResult };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));
        var request = NewValidRequest();

        var result = await controller.CalculateNutrition(request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<NutritionResponseDto>>(ok.Value);
        Assert.Equal(0, response.Code);
        Assert.NotNull(response.Data);
        Assert.True(response.Data.Success);
        Assert.NotNull(response.Data.Nutrition);
        Assert.Equal(200, response.Data.Nutrition.Calories);
        Assert.Contains("2 ingredient(s)", response.Message);
    }

    [Fact]
    public async Task CalculateNutrition_ReturnsBadRequest_WithDefaultErrorMessage_WhenNoErrorMessage()
    {
        var failedResult = new NutritionResponseDto(false, null, []);
        var fakeService = new FakeNutritionService { CalculationResult = failedResult };
        var controller = CreateController(fakeService, BuildPrincipal("clerk_123"));
        var request = NewValidRequest();

        var result = await controller.CalculateNutrition(request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<NutritionResponseDto>>(badRequest.Value);
        Assert.Equal(400, response.Code);
        Assert.Equal("Calculation failed.", response.Message);
    }

    #endregion

    private static NutritionController CreateController(INutritionService service, ClaimsPrincipal user) =>
        new(service, NullLogger<NutritionController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            }
        };

    private static ClaimsPrincipal BuildPrincipal(string clerkUserId)
    {
        var identity = new ClaimsIdentity([new Claim("clerk_user_id", clerkUserId)], "mock");
        return new ClaimsPrincipal(identity);
    }

    private static CalculateNutritionRequestDto NewValidRequest() =>
        new(
            [
                new NutritionIngredientDto("Tomatoes", 2, "pcs"),
                new NutritionIngredientDto("Onions", 1, "pcs")
            ],
            2
        );

    private sealed class FakeNutritionService : INutritionService
    {
        public NutritionResponseDto? CalculationResult { get; set; }

        public Task<NutritionResponseDto> CalculateNutritionAsync(CalculateNutritionRequestDto request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CalculationResult ?? new NutritionResponseDto(true, null, []));
        }
    }
}
