using backend.Dtos;
using backend.Dtos.Vision;
using backend.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/vision")]
[Authorize]
public class VisionController : ControllerBase
{
    private readonly IVisionService _visionService;
    private readonly ILogger<VisionController> _logger;

    public VisionController(
        IVisionService visionService,
        ILogger<VisionController> logger)
    {
        _visionService = visionService;
        _logger = logger;
    }

    /// <summary>
    /// Recognize ingredients from an uploaded image.
    /// </summary>
    [HttpPost("recognize-ingredients")]
    [ProducesResponseType(typeof(ApiResponse<IngredientRecognitionResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<IngredientRecognitionResponseDto>>> RecognizeIngredients(
        IFormFile image,
        CancellationToken cancellationToken)
    {
        if (image == null || image.Length == 0)
        {
            return BadRequest(ApiResponse.Fail(400, "No image provided."));
        }

        _logger.LogInformation(
            "Ingredient recognition request. File: {FileName}, Size: {Size}, Type: {ContentType}",
            image.FileName, image.Length, image.ContentType);

        await using var stream = image.OpenReadStream();
        var result = await _visionService.RecognizeIngredientsAsync(
            stream,
            image.FileName,
            image.ContentType,
            cancellationToken);

        if (!result.Success)
        {
            return BadRequest(ApiResponse<IngredientRecognitionResponseDto>.Fail(
                400,
                result.ErrorMessage ?? "Recognition failed."));
        }

        var ingredientDtos = result.Ingredients.Select(i => new RecognizedIngredientDto(
            i.Name,
            i.Quantity,
            i.Unit,
            i.Confidence,
            i.SuggestedStorageMethod,
            i.SuggestedExpirationDays,
            i.OriginalReceiptText)).ToList();

        var filteredItemDtos = result.FilteredItems?.Select(f => new FilteredItemDto(
            f.Text,
            f.Reason)).ToList();

        var response = new IngredientRecognitionResponseDto(
            result.Success,
            result.ImageType,
            ingredientDtos,
            result.StoreName,
            filteredItemDtos,
            result.Notes,
            result.ErrorMessage);

        var message = result.ImageType == "receipt"
            ? $"Scanned receipt: {response.Ingredients.Count} grocery item(s) found."
            : $"Recognized {response.Ingredients.Count} ingredient(s).";

        return Ok(ApiResponse<IngredientRecognitionResponseDto>.Success(response, message: message));
    }

    /// <summary>
    /// Recognize a recipe from an uploaded dish image.
    /// </summary>
    [HttpPost("recognize-recipe")]
    [ProducesResponseType(typeof(ApiResponse<RecipeRecognitionResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<RecipeRecognitionResponseDto>>> RecognizeRecipe(
        IFormFile image,
        CancellationToken cancellationToken)
    {
        if (image == null || image.Length == 0)
        {
            return BadRequest(ApiResponse.Fail(400, "No image provided."));
        }

        _logger.LogInformation(
            "Recipe recognition request. File: {FileName}, Size: {Size}, Type: {ContentType}",
            image.FileName, image.Length, image.ContentType);

        await using var stream = image.OpenReadStream();
        var result = await _visionService.RecognizeRecipeAsync(
            stream,
            image.FileName,
            image.ContentType,
            cancellationToken);

        if (!result.Success)
        {
            return BadRequest(ApiResponse<RecipeRecognitionResponseDto>.Fail(
                400,
                result.ErrorMessage ?? "Recognition failed."));
        }

        RecognizedRecipeDto? recipeDto = null;
        if (result.Recipe != null)
        {
            RecipeNutritionDto? nutritionDto = null;
            if (result.Recipe.Nutrition != null)
            {
                nutritionDto = new RecipeNutritionDto(
                    result.Recipe.Nutrition.Calories,
                    result.Recipe.Nutrition.Carbohydrates,
                    result.Recipe.Nutrition.Fat,
                    result.Recipe.Nutrition.Protein,
                    result.Recipe.Nutrition.Sugar,
                    result.Recipe.Nutrition.Sodium,
                    result.Recipe.Nutrition.SaturatedFat);
            }

            recipeDto = new RecognizedRecipeDto(
                result.Recipe.Title,
                result.Recipe.Description,
                result.Recipe.Ingredients.Select(i => new RecipeIngredientDto(
                    i.Name,
                    i.Quantity,
                    i.Unit,
                    i.Category)).ToList(),
                result.Recipe.Steps,
                result.Recipe.PrepTimeMinutes,
                result.Recipe.CookTimeMinutes,
                result.Recipe.Servings,
                result.Recipe.Confidence,
                nutritionDto);
        }

        var response = new RecipeRecognitionResponseDto(
            result.Success,
            recipeDto,
            result.ErrorMessage);

        return Ok(ApiResponse<RecipeRecognitionResponseDto>.Success(
            response,
            message: recipeDto != null ? $"Recognized recipe: {recipeDto.Title}" : "Recognition completed."));
    }

    /// <summary>
    /// Generate recipe content (steps, tags, ingredients) from images and context.
    /// </summary>
    [HttpPost("generate-recipe-content")]
    [ProducesResponseType(typeof(ApiResponse<GenerateRecipeContentResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<GenerateRecipeContentResponseDto>>> GenerateRecipeContent(
        [FromBody] GenerateRecipeContentRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.ImageUrls == null || request.ImageUrls.Count == 0)
        {
            return BadRequest(ApiResponse.Fail(400, "At least one image URL is required."));
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest(ApiResponse.Fail(400, "Recipe title is required."));
        }

        _logger.LogInformation(
            "Recipe content generation request. Title: {Title}, Images: {Count}",
            request.Title, request.ImageUrls.Count);

        var result = await _visionService.GenerateRecipeContentAsync(
            request.ImageUrls,
            request.Title,
            request.Description,
            cancellationToken);

        if (!result.Success)
        {
            return BadRequest(ApiResponse<GenerateRecipeContentResponseDto>.Fail(
                400,
                result.ErrorMessage ?? "Content generation failed."));
        }

        var ingredientDtos = result.Ingredients?.Select(i => new GeneratedIngredientDto(
            i.Name,
            i.Amount,
            i.Unit,
            i.Category)).ToList();

        var response = new GenerateRecipeContentResponseDto(
            result.Success,
            result.Description,
            result.Steps,
            result.Tags,
            ingredientDtos,
            result.Confidence,
            result.ErrorMessage);

        return Ok(ApiResponse<GenerateRecipeContentResponseDto>.Success(
            response,
            message: $"Generated {result.Steps?.Count ?? 0} steps and {result.Tags?.Count ?? 0} tags."));
    }
}
