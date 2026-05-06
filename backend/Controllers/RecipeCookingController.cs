using System.Text.Json;
using backend.Dtos;
using backend.Dtos.Recipes;
using backend.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/recipes/{recipeId:guid}/cook")]
[Authorize]
public class RecipeCookingController(
    IRecipeRepository recipeRepository,
    ILogger<RecipeCookingController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<CookingSessionDto>>> GetAsync(Guid recipeId,
        CancellationToken cancellationToken)
    {
        var recipe = await recipeRepository.GetByIdAsync(recipeId, cancellationToken);
        if (recipe is null)
        {
            return NotFound(ApiResponse<CookingSessionDto>.Fail(404, "Recipe not found."));
        }

        var steps = ParseSteps(recipeId, recipe.Steps);
        var dto = new CookingSessionDto(recipe.Id, recipe.Title, steps.Count, steps);
        logger.LogInformation("Successfully retrieved recipe {RecipeId} with {StepCount} steps.", recipeId,
            steps.Count);

        return Ok(ApiResponse<CookingSessionDto>.Success(dto));
    }

    private IReadOnlyList<CookingStepDto> ParseSteps(Guid recipeId, string stepsJson)
    {
        if (string.IsNullOrWhiteSpace(stepsJson))
        {
            return Array.Empty<CookingStepDto>();
        }

        try
        {
            using var document = JsonDocument.Parse(stepsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<CookingStepDto>();
            }

            var steps = new List<CookingStepDto>();
            var fallbackOrder = 1;

            foreach (var element in document.RootElement.EnumerateArray())
            {
                int order = fallbackOrder;
                string instruction = string.Empty;
                int? suggestedSeconds = null;

                if (element.ValueKind == JsonValueKind.String)
                {
                    instruction = element.GetString() ?? string.Empty;
                }
                else if (element.ValueKind == JsonValueKind.Object)
                {
                    if (element.TryGetProperty("order", out var orderProp) && orderProp.TryGetInt32(out var orderValue) &&
                        orderValue > 0)
                    {
                        order = orderValue;
                    }

                    if (element.TryGetProperty("instruction", out var instructionProp))
                    {
                        instruction = instructionProp.GetString() ?? string.Empty;
                    }
                    else if (element.TryGetProperty("text", out var textProp))
                    {
                        instruction = textProp.GetString() ?? string.Empty;
                    }

                    if (element.TryGetProperty("suggestedSeconds", out var suggestedSecondsProp) &&
                        suggestedSecondsProp.TryGetInt32(out var suggestedSecondsValue))
                    {
                        suggestedSeconds = suggestedSecondsValue;
                    }
                    else if (element.TryGetProperty("durationSeconds", out var durationSecondsProp) &&
                        durationSecondsProp.TryGetInt32(out var durationSeconds))
                    {
                        suggestedSeconds = durationSeconds;
                    }
                    else if (element.TryGetProperty("durationMinutes", out var durationMinutesProp) &&
                             durationMinutesProp.TryGetInt32(out var durationMinutes))
                    {
                        suggestedSeconds = durationMinutes * 60;
                    }
                }

                instruction = instruction.Trim();
                if (string.IsNullOrWhiteSpace(instruction))
                {
                    fallbackOrder++;
                    continue;
                }

                steps.Add(new CookingStepDto(order, instruction, suggestedSeconds));
                fallbackOrder++;
            }

            return steps
                .OrderBy(s => s.Order)
                .ThenBy(s => s.Instruction, StringComparer.Ordinal)
                .ToList();
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to parse steps JSON for recipe {RecipeId}.", recipeId);
            return Array.Empty<CookingStepDto>();
        }
    }
}
