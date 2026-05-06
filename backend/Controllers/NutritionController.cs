using backend.Dtos;
using backend.Dtos.Recipes;
using backend.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/nutrition")]
[Authorize]
public class NutritionController : ControllerBase
{
    private readonly INutritionService _nutritionService;
    private readonly ILogger<NutritionController> _logger;

    public NutritionController(
        INutritionService nutritionService,
        ILogger<NutritionController> logger)
    {
        _nutritionService = nutritionService;
        _logger = logger;
    }

    /// <summary>
    /// Calculate nutrition information from a list of ingredients.
    /// </summary>
    [HttpPost("calculate")]
    [ProducesResponseType(typeof(ApiResponse<NutritionResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<NutritionResponseDto>>> CalculateNutrition(
        [FromBody] CalculateNutritionRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.Ingredients == null || request.Ingredients.Count == 0)
        {
            return BadRequest(ApiResponse<NutritionResponseDto>.Fail(
                400, "No ingredients provided."));
        }

        _logger.LogInformation(
            "Nutrition calculation request for {Count} ingredients",
            request.Ingredients.Count);

        var result = await _nutritionService.CalculateNutritionAsync(
            request, cancellationToken);

        if (!result.Success)
        {
            return BadRequest(ApiResponse<NutritionResponseDto>.Fail(
                400, result.ErrorMessage ?? "Calculation failed."));
        }

        return Ok(ApiResponse<NutritionResponseDto>.Success(
            result,
            message: $"Calculated nutrition for {request.Ingredients.Count} ingredient(s)."));
    }
}
