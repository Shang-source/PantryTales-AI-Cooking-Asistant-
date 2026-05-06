using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using backend.Interfaces;
using backend.Options;
using Microsoft.Extensions.Options;

namespace backend.Services.Vision;

/// <summary>
/// OpenAI Vision provider using GPT-4o for image recognition.
/// </summary>
public class OpenAIVisionProvider : IVisionProvider
{
    private readonly HttpClient _httpClient;
    private readonly VisionOptions _options;
    private readonly ILogger<OpenAIVisionProvider> _logger;

    public string ProviderName => "OpenAI";

    public OpenAIVisionProvider(
        HttpClient httpClient,
        IOptions<VisionOptions> options,
        ILogger<OpenAIVisionProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        if (string.IsNullOrEmpty(_options.ApiKey))
        {
            _logger.LogWarning("OpenAI API key is not configured. Vision recognition will fail.");
        }
    }

    public async Task<IngredientRecognitionResult> RecognizeIngredientsAsync(
        byte[] imageData,
        string mimeType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var prompt = """
                Analyze this image and determine if it is:
                1. A RECEIPT/BILL (printed text, line items, prices, store name)
                2. A PHOTO OF ACTUAL INGREDIENTS (physical food items, groceries)

                === FOR RECEIPTS ===
                Extract ONLY food/grocery items. FILTER OUT:

                NON-FOOD TO EXCLUDE:
                - Bags (BAG, PLSTC BAG, CARRIER, REUSE BAG)
                - Cleaning (BLEACH, DETERGENT, SOAP, CLEANER, WIPES, SPONGE)
                - Paper (PAPER TOWEL, TOILET PAPER, TISSUES, NAPKINS)
                - Personal care (SHAMPOO, TOOTHPASTE, DEODORANT, LOTION)
                - Pet supplies (DOG FOOD, CAT FOOD, PET TREATS)
                - Household (BATTERIES, TRASH BAGS, LIGHT BULBS)

                FEES TO EXCLUDE:
                - TAX, SALES TAX, VAT, GST, HST
                - DISCOUNT, COUPON, SAVINGS, PROMO
                - SUBTOTAL, TOTAL, BALANCE, TENDER, DUE
                - BAG FEE, DEPOSIT, SERVICE FEE

                EXPAND ABBREVIATIONS:
                - ORG = Organic, BNLS = Boneless, GRD = Ground, SKNLS = Skinless
                - CHKN/CKN = Chicken, BF = Beef, PRK = Pork, TKY = Turkey
                - GRN = Green, YLW = Yellow, WHT = White, BRN = Brown
                - MLK = Milk, CHSE = Cheese, YGT = Yogurt, BTR = Butter
                - LB = pound (convert to ~454g), OZ = ounce (convert to ~28g)
                - GAL = gallon (convert to ~3.78l), QT = quart (convert to ~946ml)
                - PCS/EA = pieces, DZ = dozen, BNCH = bunch, CT = count

                === FOR INGREDIENT PHOTOS ===
                Identify visible food items with:
                - Specific names (e.g., "Roma tomato" not just "tomato")
                - Estimated quantity and unit
                - Suggested storage method and expiration days

                === RESPONSE FORMAT ===
                Return ONLY valid JSON:
                {
                  "imageType": "receipt" | "ingredients" | "unknown",
                  "storeName": "string or null (only for receipts)",
                  "ingredients": [
                    {
                      "name": "string",
                      "quantity": number,
                      "unit": "g|kg|ml|l|pcs|cups|tbsp|tsp",
                      "confidence": 0.0-1.0,
                      "suggestedStorageMethod": "Fridge|Freezer|Pantry|Counter",
                      "suggestedExpirationDays": number,
                      "originalReceiptText": "string or null (raw text for receipts)"
                    }
                  ],
                  "filteredItems": [
                    {"text": "string", "reason": "non-food|tax|fee|discount"}
                  ],
                  "notes": "string or null"
                }

                If no food items found: {"imageType": "...", "ingredients": [], "filteredItems": [...], "notes": "..."}
                """;

            var response = await CallOpenAIVisionAsync(imageData, mimeType, prompt, cancellationToken);
            return ParseIngredientResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to recognize ingredients from image.");
            return new IngredientRecognitionResult(false, "unknown", [], ErrorMessage: ex.Message);
        }
    }

    public async Task<RecipeRecognitionResult> RecognizeRecipeAsync(
        byte[] imageData,
        string mimeType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var prompt = """
                Analyze this image and generate a recipe for the dish shown.
                
                IMPORTANT: Always attempt to identify the dish based on visible ingredients, colors, textures, and cooking style. 
                Even if you're not 100% certain, provide your best guess with a lower confidence score (0.3-0.7).
                Only return an error if the image clearly contains no food at all.
                
                Include:
                - Title (name of the dish - be specific, e.g., "Stir-Fried Eggs with Wood Ear Mushrooms and Peppers")
                - Description (brief description of the dish)
                - List of ingredients with name, quantity, unit, and category (must be one of: Vegetables, Fruits, Meat, Seafood, Dairy, Grains, Spices, Beverages, Snacks, Other)
                - Step-by-step cooking instructions
                - Prep time in minutes
                - Cook time in minutes
                - Number of servings
                - Your confidence level (0.0 to 1.0)
                - Estimated nutrition values per serving (approximate values are fine)

                Return ONLY valid JSON in this exact format:
                {
                  "title": "string",
                  "description": "string",
                  "ingredients": [
                    {
                      "name": "string",
                      "quantity": number,
                      "unit": "string",
                      "category": "string"
                    }
                  ],
                  "steps": ["string"],
                  "prepTimeMinutes": number,
                  "cookTimeMinutes": number,
                  "servings": number,
                  "confidence": number,
                  "nutrition": {
                    "calories": number,
                    "carbohydrates": number,
                    "fat": number,
                    "protein": number,
                    "sugar": number,
                    "sodium": number,
                    "saturatedFat": number
                  }
                }

                Note: "unit" must be one of: g, kg, ml, l, pcs, cups, tbsp, tsp. Convert any non-standard or locale-specific units to the closest standard unit with adjusted quantity.
                Only if the image contains no food at all, return: {"error": "No food detected in image"}
                """;

            var response = await CallOpenAIVisionAsync(imageData, mimeType, prompt, cancellationToken);
            return ParseRecipeResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to recognize recipe from image.");
            return new RecipeRecognitionResult(false, null, ex.Message);
        }
    }

    public async Task<GenerateRecipeContentResult> GenerateRecipeContentAsync(
        List<byte[]> imagesData,
        List<string> mimeTypes,
        string title,
        string? description,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var descriptionPart = string.IsNullOrWhiteSpace(description)
                ? ""
                : $"\n- Description: \"{description}\"";

            var prompt = $$"""
                You are a culinary expert analyzing food images to help create a recipe post (like Xiaohongshu/Red Note style).

                CONTEXT:
                - Recipe Title: "{{title}}"{{descriptionPart}}
                - Number of images: {{imagesData.Count}}

                LANGUAGE REQUIREMENT (CRITICAL):
                - Detect the language used in the Recipe Title above
                - Generate ALL content (description, steps, ingredient names) in the SAME language as the title
                - For example: if title is "番茄炒蛋", write everything in Chinese
                - If title is "Tomato Scrambled Eggs", write everything in English
                - Tags should remain in English PascalCase for consistency

                TASK: Analyze the provided image(s) of this dish and generate:
                1. DESCRIPTION: An engaging, personal description (2-4 sentences) that tells the story of the dish
                2. STEPS: Detailed cooking instructions (5-12 steps typically)
                3. TAGS: Relevant recipe tags (3-6 tags)
                4. INGREDIENTS: Complete ingredient list with quantities

                GUIDELINES FOR DESCRIPTION:
                - Write in a warm, personal tone like sharing with friends
                - Mention what makes this dish special or when you like to make it
                - Keep it concise but engaging (50-150 characters ideal)
                - Can include emojis sparingly for warmth

                GUIDELINES FOR STEPS:
                - Write in imperative form ("Chop the onions" / "切洋葱")
                - Each step should be one clear action
                - Include cooking times and temperatures where relevant
                - Order steps logically (prep -> cook -> serve)

                GUIDELINES FOR TAGS:
                - Include cuisine type (Chinese, Italian, Mexican, etc.)
                - Include meal type (Breakfast, Lunch, Dinner, Snack, etc.)
                - Include dietary info if visible (Vegetarian, GlutenFree, LowCarb, etc.)
                - Include cooking style (QuickMeal, SlowCooked, OnePot, etc.)
                - Use PascalCase for multi-word tags (e.g., "QuickMeal" not "Quick Meal")

                GUIDELINES FOR INGREDIENTS:
                - Be specific about quantities
                - Use standard units: g, kg, ml, l, pcs, cups, tbsp, tsp
                - Ingredient names should match the detected language
                - Include category from: Vegetables, Fruits, Meat, Seafood, Dairy, Grains, Spices, Beverages, Snacks, Other

                RESPONSE FORMAT (JSON only):
                {
                  "description": "...",
                  "steps": ["Step 1...", "Step 2...", ...],
                  "tags": ["Tag1", "Tag2", ...],
                  "ingredients": [
                    {"name": "...", "amount": number, "unit": "...", "category": "..."}
                  ],
                  "confidence": 0.0-1.0
                }

                If the images do not clearly show food or don't match the title:
                {"error": "Cannot generate recipe content from these images", "confidence": 0}
                """;

            var response = await CallOpenAIVisionMultiImageAsync(imagesData, mimeTypes, prompt, cancellationToken);
            return ParseGenerateContentResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate recipe content from images.");
            return new GenerateRecipeContentResult(false, null, null, null, null, null, ex.Message);
        }
    }

    private async Task<string> CallOpenAIVisionMultiImageAsync(
        List<byte[]> imagesData,
        List<string> mimeTypes,
        string prompt,
        CancellationToken cancellationToken)
    {
        var url = $"{_options.BaseUrl}/chat/completions";

        var content = new List<object> { new OpenAITextContent { Text = prompt } };

        for (var i = 0; i < imagesData.Count; i++)
        {
            var base64Image = Convert.ToBase64String(imagesData[i]);
            var mimeType = i < mimeTypes.Count ? mimeTypes[i] : "image/jpeg";
            var dataUri = $"data:{mimeType};base64,{base64Image}";
            content.Add(new OpenAIImageContent { ImageUrl = new OpenAIImageUrl { Url = dataUri } });
        }

        var request = new OpenAIRequest
        {
            Model = _options.Model,
            Messages =
            [
                new OpenAIMessage
                {
                    Role = "user",
                    Content = content
                }
            ],
            MaxTokens = 3000,
            ResponseFormat = new OpenAIResponseFormat { Type = "json_object" }
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(request, JsonOptions),
                Encoding.UTF8,
                "application/json")
        };
        httpRequest.Headers.Add("Authorization", $"Bearer {_options.ApiKey}");

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var safeError = errorContent.Length > 200 ? errorContent[..200] + "..." : errorContent;
            _logger.LogError("OpenAI API error: {StatusCode} - {Error}", response.StatusCode, safeError);
            throw new HttpRequestException($"OpenAI API error: {response.StatusCode}");
        }

        var result = await response.Content.ReadFromJsonAsync<OpenAIResponse>(JsonOptions, cancellationToken);

        if (result?.Choices == null || result.Choices.Count == 0)
        {
            throw new InvalidOperationException("OpenAI returned empty response.");
        }

        var textContent = result.Choices[0].Message?.Content;

        if (string.IsNullOrEmpty(textContent))
        {
            throw new InvalidOperationException("OpenAI returned no text content.");
        }

        return textContent;
    }

    private async Task<string> CallOpenAIVisionAsync(
        byte[] imageData,
        string mimeType,
        string prompt,
        CancellationToken cancellationToken)
    {
        var url = $"{_options.BaseUrl}/chat/completions";

        var base64Image = Convert.ToBase64String(imageData);
        var dataUri = $"data:{mimeType};base64,{base64Image}";

        var request = new OpenAIRequest
        {
            Model = _options.Model,
            Messages =
            [
                new OpenAIMessage
                {
                    Role = "user",
                    Content =
                    [
                        new OpenAITextContent { Text = prompt },
                        new OpenAIImageContent
                        {
                            ImageUrl = new OpenAIImageUrl { Url = dataUri }
                        }
                    ]
                }
            ],
            MaxTokens = 2048,
            ResponseFormat = new OpenAIResponseFormat { Type = "json_object" }
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(request, JsonOptions),
                Encoding.UTF8,
                "application/json")
        };
        httpRequest.Headers.Add("Authorization", $"Bearer {_options.ApiKey}");

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var safeError = errorContent.Length > 200 ? errorContent[..200] + "..." : errorContent;
            _logger.LogError("OpenAI API error: {StatusCode} - {Error}", response.StatusCode, safeError);
            throw new HttpRequestException($"OpenAI API error: {response.StatusCode}");
        }

        var result = await response.Content.ReadFromJsonAsync<OpenAIResponse>(JsonOptions, cancellationToken);

        if (result?.Choices == null || result.Choices.Count == 0)
        {
            throw new InvalidOperationException("OpenAI returned empty response.");
        }

        var textContent = result.Choices[0].Message?.Content;

        if (string.IsNullOrEmpty(textContent))
        {
            throw new InvalidOperationException("OpenAI returned no text content.");
        }

        return textContent;
    }

    private IngredientRecognitionResult ParseIngredientResponse(string response)
    {
        try
        {
            var cleanedResponse = CleanJsonResponse(response);

            var parsed = JsonSerializer.Deserialize<IngredientJsonResponse>(cleanedResponse, JsonOptions);

            var imageType = parsed?.ImageType ?? "unknown";
            var storeName = parsed?.StoreName;
            var notes = parsed?.Notes;

            var ingredients = (parsed?.Ingredients ?? [])
                .Select(i => new RecognizedIngredient(
                    i.Name ?? "Unknown",
                    i.Quantity,
                    i.Unit ?? "pcs",
                    i.Confidence,
                    i.SuggestedStorageMethod,
                    i.SuggestedExpirationDays,
                    i.OriginalReceiptText))
                .ToList();

            var filteredItems = (parsed?.FilteredItems ?? [])
                .Select(f => new FilteredItem(f.Text ?? "", f.Reason ?? "unknown"))
                .ToList();

            return new IngredientRecognitionResult(
                Success: true,
                ImageType: imageType,
                Ingredients: ingredients,
                StoreName: storeName,
                FilteredItems: filteredItems.Count > 0 ? filteredItems : null,
                Notes: notes);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse ingredient JSON response: {Response}", response);
            return new IngredientRecognitionResult(false, "unknown", [], ErrorMessage: "Failed to parse AI response.");
        }
    }

    private RecipeRecognitionResult ParseRecipeResponse(string response)
    {
        try
        {
            var cleanedResponse = CleanJsonResponse(response);

            var parsed = JsonSerializer.Deserialize<RecipeJsonResponse>(cleanedResponse, JsonOptions);

            if (parsed?.Error != null)
            {
                _logger.LogWarning("AI returned error: {Error}", parsed.Error);
                return new RecipeRecognitionResult(false, null, parsed.Error);
            }

            if (string.IsNullOrEmpty(parsed?.Title))
            {
                _logger.LogWarning("Parsed recipe has no title. Raw parsed object: Title={Title}, Ingredients={IngCount}, Steps={StepCount}",
                    parsed?.Title, parsed?.Ingredients?.Count, parsed?.Steps?.Count);
                return new RecipeRecognitionResult(false, null, "Failed to parse recipe.");
            }

            var ingredients = (parsed.Ingredients ?? [])
                .Select(i => new VisionRecipeIngredient(
                    i.Name ?? "Unknown",
                    i.Quantity,
                    i.Unit ?? "pcs",
                    i.Category))
                .ToList();

            RecipeNutrition? nutrition = null;
            if (parsed.Nutrition != null)
            {
                nutrition = new RecipeNutrition(
                    parsed.Nutrition.Calories,
                    parsed.Nutrition.Carbohydrates,
                    parsed.Nutrition.Fat,
                    parsed.Nutrition.Protein,
                    parsed.Nutrition.Sugar,
                    parsed.Nutrition.Sodium,
                    parsed.Nutrition.SaturatedFat);
            }

            var recipe = new RecognizedRecipe(
                parsed.Title,
                parsed.Description ?? "",
                ingredients,
                parsed.Steps ?? [],
                parsed.PrepTimeMinutes,
                parsed.CookTimeMinutes,
                parsed.Servings,
                parsed.Confidence,
                nutrition);

            return new RecipeRecognitionResult(true, recipe);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse recipe JSON response: {Response}", response);
            return new RecipeRecognitionResult(false, null, "Failed to parse AI response.");
        }
    }

    private GenerateRecipeContentResult ParseGenerateContentResponse(string response)
    {
        try
        {
            var cleanedResponse = CleanJsonResponse(response);

            var parsed = JsonSerializer.Deserialize<GenerateContentJsonResponse>(cleanedResponse, JsonOptions);

            if (parsed?.Error != null)
            {
                _logger.LogWarning("AI returned error: {Error}", parsed.Error);
                return new GenerateRecipeContentResult(false, null, null, null, null, parsed.Confidence, parsed.Error);
            }

            var ingredients = (parsed?.Ingredients ?? [])
                .Select(i => new VisionGeneratedIngredient(
                    i.Name ?? "Unknown",
                    i.Amount,
                    i.Unit,
                    i.Category))
                .ToList();

            return new GenerateRecipeContentResult(
                Success: true,
                Description: parsed?.Description,
                Steps: parsed?.Steps,
                Tags: parsed?.Tags,
                Ingredients: ingredients,
                Confidence: parsed?.Confidence);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse generate content JSON response: {Response}", response);
            return new GenerateRecipeContentResult(false, null, null, null, null, null, "Failed to parse AI response.");
        }
    }

    private static string CleanJsonResponse(string response)
    {
        var cleaned = response.Trim();

        if (cleaned.StartsWith("```json"))
        {
            cleaned = cleaned[7..];
        }
        else if (cleaned.StartsWith("```"))
        {
            cleaned = cleaned[3..];
        }

        if (cleaned.EndsWith("```"))
        {
            cleaned = cleaned[..^3];
        }

        // Convert fractions like 1/2 to decimal values (e.g., 0.5)
        // Matches patterns like: 1/2, 1/4, 3/4 that appear as JSON number values
        cleaned = System.Text.RegularExpressions.Regex.Replace(
            cleaned.Trim(),
            @":\s*(\d+)/(\d+)\s*([,}\]])",
            match =>
            {
                var numerator = double.Parse(match.Groups[1].Value);
                var denominatorInt = int.Parse(match.Groups[2].Value);
                var result = denominatorInt != 0 ? numerator / denominatorInt : 0;
                return $": {result}{match.Groups[3].Value}";
            });

        return cleaned.Trim();
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    #region OpenAI API Models

    private sealed class OpenAIRequest
    {
        [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;
        [JsonPropertyName("messages")] public List<OpenAIMessage> Messages { get; set; } = [];
        [JsonPropertyName("max_tokens")] public int MaxTokens { get; set; }
        [JsonPropertyName("response_format")] public OpenAIResponseFormat? ResponseFormat { get; set; }
    }

    private sealed class OpenAIResponseFormat
    {
        [JsonPropertyName("type")] public string Type { get; set; } = "json_object";
    }

    private sealed class OpenAIMessage
    {
        [JsonPropertyName("role")] public string Role { get; set; } = string.Empty;
        [JsonPropertyName("content")] public List<object> Content { get; set; } = [];
    }

    private sealed class OpenAITextContent
    {
        [JsonPropertyName("type")] public string Type => "text";
        [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;
    }

    private sealed class OpenAIImageContent
    {
        [JsonPropertyName("type")] public string Type => "image_url";
        [JsonPropertyName("image_url")] public OpenAIImageUrl ImageUrl { get; set; } = new();
    }

    private sealed class OpenAIImageUrl
    {
        [JsonPropertyName("url")] public string Url { get; set; } = string.Empty;
    }

    private sealed class OpenAIResponse
    {
        [JsonPropertyName("choices")] public List<OpenAIChoice> Choices { get; set; } = [];
    }

    private sealed class OpenAIChoice
    {
        [JsonPropertyName("message")] public OpenAIResponseMessage? Message { get; set; }
    }

    private sealed class OpenAIResponseMessage
    {
        [JsonPropertyName("content")] public string? Content { get; set; }
    }

    #endregion

    #region Response Parsing Models

    private sealed class IngredientJsonResponse
    {
        [JsonPropertyName("imageType")] public string? ImageType { get; set; }
        [JsonPropertyName("storeName")] public string? StoreName { get; set; }
        [JsonPropertyName("ingredients")] public List<IngredientJson>? Ingredients { get; set; }
        [JsonPropertyName("filteredItems")] public List<FilteredItemJson>? FilteredItems { get; set; }
        [JsonPropertyName("notes")] public string? Notes { get; set; }
    }

    private sealed class IngredientJson
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("quantity")] public decimal Quantity { get; set; }
        [JsonPropertyName("unit")] public string? Unit { get; set; }
        [JsonPropertyName("confidence")] public double Confidence { get; set; }
        [JsonPropertyName("suggestedStorageMethod")] public string? SuggestedStorageMethod { get; set; }
        [JsonPropertyName("suggestedExpirationDays")] public int? SuggestedExpirationDays { get; set; }
        [JsonPropertyName("originalReceiptText")] public string? OriginalReceiptText { get; set; }
    }

    private sealed class FilteredItemJson
    {
        [JsonPropertyName("text")] public string? Text { get; set; }
        [JsonPropertyName("reason")] public string? Reason { get; set; }
    }

    private sealed class RecipeJsonResponse
    {
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("ingredients")] public List<RecipeIngredientJson>? Ingredients { get; set; }
        [JsonPropertyName("steps")] public List<string>? Steps { get; set; }
        [JsonPropertyName("prepTimeMinutes")] public int? PrepTimeMinutes { get; set; }
        [JsonPropertyName("cookTimeMinutes")] public int? CookTimeMinutes { get; set; }
        [JsonPropertyName("servings")] public int? Servings { get; set; }
        [JsonPropertyName("confidence")] public double Confidence { get; set; }
        [JsonPropertyName("error")] public string? Error { get; set; }
        [JsonPropertyName("nutrition")] public NutritionJson? Nutrition { get; set; }
    }

    private sealed class NutritionJson
    {
        [JsonPropertyName("calories")] public decimal? Calories { get; set; }
        [JsonPropertyName("carbohydrates")] public decimal? Carbohydrates { get; set; }
        [JsonPropertyName("fat")] public decimal? Fat { get; set; }
        [JsonPropertyName("protein")] public decimal? Protein { get; set; }
        [JsonPropertyName("sugar")] public decimal? Sugar { get; set; }
        [JsonPropertyName("sodium")] public decimal? Sodium { get; set; }
        [JsonPropertyName("saturatedFat")] public decimal? SaturatedFat { get; set; }
    }

    private sealed class RecipeIngredientJson
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("quantity")] public decimal Quantity { get; set; }
        [JsonPropertyName("unit")] public string? Unit { get; set; }
        [JsonPropertyName("category")] public string? Category { get; set; }
    }

    private sealed class GenerateContentJsonResponse
    {
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("steps")] public List<string>? Steps { get; set; }
        [JsonPropertyName("tags")] public List<string>? Tags { get; set; }
        [JsonPropertyName("ingredients")] public List<GeneratedIngredientJson>? Ingredients { get; set; }
        [JsonPropertyName("confidence")] public double? Confidence { get; set; }
        [JsonPropertyName("error")] public string? Error { get; set; }
    }

    private sealed class GeneratedIngredientJson
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("amount")] public decimal? Amount { get; set; }
        [JsonPropertyName("unit")] public string? Unit { get; set; }
        [JsonPropertyName("category")] public string? Category { get; set; }
    }

    #endregion
}
