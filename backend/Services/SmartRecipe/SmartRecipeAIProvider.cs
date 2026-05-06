using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using backend.Interfaces;
using backend.Options;
using Microsoft.Extensions.Options;

namespace backend.Services.SmartRecipe;

/// <summary>
/// OpenAI-based AI provider for generating smart recipe suggestions.
/// </summary>
public class SmartRecipeAIProvider : ISmartRecipeAIProvider
{
    private readonly HttpClient _httpClient;
    private readonly VisionOptions _options;
    private readonly ILogger<SmartRecipeAIProvider> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    public SmartRecipeAIProvider(
        HttpClient httpClient,
        IOptions<VisionOptions> options,
        ILogger<SmartRecipeAIProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<SmartRecipeAIResponse> GenerateRecipesAsync(
        SmartRecipeAIRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var prompt = BuildPrompt(request);

            var requestBody = new
            {
                model = _options.Model ?? "gpt-4o-mini",
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = """
                            You are a professional chef and meal planner. Generate creative, practical recipes 
                            based on the user's available ingredients, preferences, and dietary needs.
                            Always respond with valid JSON matching the specified schema.
                            """
                    },
                    new
                    {
                        role = "user",
                        content = prompt
                    }
                },
                response_format = new { type = "json_object" },
                max_tokens = 1500,
                temperature = 0.4
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
            httpRequest.Headers.Add("Authorization", $"Bearer {_options.ApiKey}");
            httpRequest.Content = new StringContent(
                JsonSerializer.Serialize(requestBody, JsonOptions),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("OpenAI API error: {Status} - {Content}", response.StatusCode, responseContent);
                return new SmartRecipeAIResponse(false, ErrorMessage: $"API error: {response.StatusCode}");
            }

            var openAiResponse = JsonSerializer.Deserialize<OpenAIResponse>(responseContent, JsonOptions);
            var content = openAiResponse?.Choices?.FirstOrDefault()?.Message?.Content;

            if (string.IsNullOrEmpty(content))
            {
                return new SmartRecipeAIResponse(false, ErrorMessage: "Empty response from AI");
            }

            // Parse the JSON response
            var recipes = ParseRecipeResponse(content);
            return new SmartRecipeAIResponse(true, recipes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate smart recipes");
            return new SmartRecipeAIResponse(false, ErrorMessage: ex.Message);
        }
    }

    public async Task<SmartRecipeAIResponse> GenerateSingleRecipeAsync(
        SmartRecipeAIRequest request,
        int recipeIndex,
        IReadOnlyList<string> previousTitles,
        string missingTarget,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var prompt = BuildSingleRecipePrompt(request, previousTitles, missingTarget);

            var requestBody = new
            {
                model = _options.Model ?? "gpt-4o-mini",
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = """
                            You are a professional chef and meal planner. Generate ONE creative, practical recipe
                            based on the user's available ingredients, preferences, and dietary needs.
                            Always respond with valid JSON matching the specified schema.
                            """
                    },
                    new
                    {
                        role = "user",
                        content = prompt
                    }
                },
                response_format = new { type = "json_object" },
                max_tokens = 800,
                temperature = 0.5
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
            httpRequest.Headers.Add("Authorization", $"Bearer {_options.ApiKey}");
            httpRequest.Content = new StringContent(
                JsonSerializer.Serialize(requestBody, JsonOptions),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("OpenAI API error for single recipe: {Status} - {Content}", response.StatusCode, responseContent);
                return new SmartRecipeAIResponse(false, ErrorMessage: $"API error: {response.StatusCode}");
            }

            var openAiResponse = JsonSerializer.Deserialize<OpenAIResponse>(responseContent, JsonOptions);
            var content = openAiResponse?.Choices?.FirstOrDefault()?.Message?.Content;

            if (string.IsNullOrEmpty(content))
            {
                return new SmartRecipeAIResponse(false, ErrorMessage: "Empty response from AI");
            }

            var recipes = ParseSingleRecipeResponse(content);
            return new SmartRecipeAIResponse(true, recipes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate single smart recipe at index {Index}", recipeIndex);
            return new SmartRecipeAIResponse(false, ErrorMessage: ex.Message);
        }
    }

    private static string BuildSingleRecipePrompt(
        SmartRecipeAIRequest request,
        IReadOnlyList<string> previousTitles,
        string missingTarget)
    {
        var sb = new StringBuilder();

        sb.AppendLine("Generate exactly ONE unique recipe based on the following context:");
        sb.AppendLine();

        sb.AppendLine("## Available Ingredients:");
        foreach (var item in request.InventoryItems)
        {
            sb.AppendLine($"- {item}");
        }
        sb.AppendLine();

        sb.AppendLine($"## Target Servings: {request.ServingsCount} people");
        sb.AppendLine();

        if (request.Preferences.Count > 0)
        {
            sb.AppendLine("## Preferences (prioritize these):");
            foreach (var pref in request.Preferences)
            {
                sb.AppendLine($"- {pref}");
            }
            sb.AppendLine();
        }

        if (request.Avoid.Count > 0)
        {
            sb.AppendLine("## MUST AVOID (allergies/restrictions):");
            foreach (var avoid in request.Avoid)
            {
                sb.AppendLine($"- {avoid}");
            }
            sb.AppendLine();
        }

        if (previousTitles.Count > 0)
        {
            sb.AppendLine("## Already Generated Recipes (DO NOT generate these again):");
            foreach (var title in previousTitles)
            {
                sb.AppendLine($"- {title}");
            }
            sb.AppendLine();
        }

        if (request.ReferenceRecipes.Count > 0)
        {
            sb.AppendLine("## Reference Recipes for Inspiration:");
            foreach (var recipe in request.ReferenceRecipes.Take(5))
            {
                sb.AppendLine($"- {recipe.Title}: {string.Join(", ", recipe.Ingredients.Take(5))}");
            }
            sb.AppendLine();
        }

        // Specify the missing ingredient target
        var missingRequirement = missingTarget == "zero"
            ? "This recipe MUST use ONLY ingredients from the available list above. Basic seasonings (salt, pepper, water, cooking oil) don't count as missing."
            : "This recipe should require 1-2 additional common ingredients that are easy to obtain.";

        sb.AppendLine("## Output Requirements:");
        sb.AppendLine("1. Generate exactly ONE recipe that is DIFFERENT from any previously generated recipes listed above.");
        sb.AppendLine($"2. {missingRequirement}");
        sb.AppendLine("3. Accurately list ALL missing ingredients that are NOT in the available ingredients list.");
        sb.AppendLine("4. Include a match score (0-1) based on how well the recipe fits preferences.");
        sb.AppendLine("5. Estimate nutrition values per serving (approximate values are fine).");
        sb.AppendLine("6. Set difficulty level appropriately:");
        sb.AppendLine("   - \"Easy\": Simple techniques (boiling, basic frying), under 30 min, fewer than 6 ingredients");
        sb.AppendLine("   - \"Medium\": Moderate techniques (sautéing, baking, marinating), 30-60 min, 6-10 ingredients");
        sb.AppendLine("   - \"Hard\": Advanced techniques (braising, deep-frying, complex sauces), over 60 min, many ingredients");
        sb.AppendLine();
        sb.AppendLine("""
            Respond with ONLY valid JSON in this exact format:
            {
              "recipe": {
                "title": "Recipe Name",
                "description": "Brief description",
                "servings": 4,
                "totalTimeMinutes": 30,
                "difficulty": "Easy",
                "ingredients": [
                  {"name": "ingredient name", "amount": 1.0, "unit": "cup", "category": "Vegetables"}
                ],
                "steps": [
                  {"order": 1, "instruction": "Step description", "suggestedSeconds": 300}
                ],
                "missingIngredients": ["ingredient1", "ingredient2"],
                "matchScore": 0.85,
                "nutrition": {
                  "calories": 350,
                  "carbohydrates": 45,
                  "fat": 12,
                  "protein": 18,
                  "sugar": 8,
                  "sodium": 580,
                  "saturatedFat": 4
                }
              }
            }

            Note: "difficulty" must be exactly one of: "Easy", "Medium", or "Hard" (case-sensitive).
            Note: "unit" must be one of: g, kg, ml, l, pcs, cups, tbsp, tsp.
            Note: "category" must be one of: Vegetables, Fruits, Meat, Seafood, Dairy, Grains, Spices, Beverages, Snacks, Other.
            Note: "title" must be in Title Case (e.g., "Chicken Stir Fry", not "chicken stir fry").
            """);

        return sb.ToString();
    }

    private List<GeneratedRecipe> ParseSingleRecipeResponse(string content)
    {
        var json = content.Trim();

        // Extract JSON from potential markdown/code fences
        if (json.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = json.IndexOf('\n');
            json = firstNewline > 0 ? json[(firstNewline + 1)..] : json.TrimStart('`');
        }

        if (json.Contains("```"))
        {
            var endFence = json.LastIndexOf("```", StringComparison.Ordinal);
            if (endFence > 0)
            {
                json = json[..endFence].Trim();
            }
        }

        var firstBrace = json.IndexOf('{');
        var lastBrace = json.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            json = json[firstBrace..(lastBrace + 1)];
        }

        try
        {
            var response = JsonSerializer.Deserialize<AISingleRecipeResponse>(json, JsonOptions);
            if (response?.Recipe == null)
            {
                return [];
            }

            var r = response.Recipe;
            return [new GeneratedRecipe(
                r.Title ?? "Untitled",
                r.Description ?? "",
                r.Servings,
                r.TotalTimeMinutes,
                r.Difficulty ?? "Easy",
                r.Ingredients?.Select(i => new GeneratedIngredient(
                    i.Name ?? "",
                    i.Amount,
                    i.Unit ?? ""
                )).ToList() ?? [],
                r.Steps?.Select(s => new GeneratedStep(
                    s.Order,
                    s.Instruction ?? "",
                    s.SuggestedSeconds
                )).ToList() ?? [],
                r.MissingIngredients ?? [],
                r.MatchScore,
                r.Nutrition?.Calories,
                r.Nutrition?.Carbohydrates,
                r.Nutrition?.Fat,
                r.Nutrition?.Protein,
                r.Nutrition?.Sugar,
                r.Nutrition?.Sodium,
                r.Nutrition?.SaturatedFat
            )];
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse single AI recipe response: {Content}", json[..Math.Min(500, json.Length)]);
            return [];
        }
    }

    private static string BuildPrompt(SmartRecipeAIRequest request)
    {
        var sb = new StringBuilder();

        sb.AppendLine("Generate up to 5 unique recipes based on the following context:");
        sb.AppendLine();

        sb.AppendLine("## Available Ingredients:");
        foreach (var item in request.InventoryItems)
        {
            sb.AppendLine($"- {item}");
        }
        sb.AppendLine();

        sb.AppendLine($"## Target Servings: {request.ServingsCount} people");
        sb.AppendLine();

        if (request.Preferences.Count > 0)
        {
            sb.AppendLine("## Preferences (prioritize these):");
            foreach (var pref in request.Preferences)
            {
                sb.AppendLine($"- {pref}");
            }
            sb.AppendLine();
        }

        if (request.Avoid.Count > 0)
        {
            sb.AppendLine("## MUST AVOID (allergies/restrictions):");
            foreach (var avoid in request.Avoid)
            {
                sb.AppendLine($"- {avoid}");
            }
            sb.AppendLine();
        }

        if (request.ReferenceRecipes.Count > 0)
        {
            sb.AppendLine("## Reference Recipes for Inspiration:");
            foreach (var recipe in request.ReferenceRecipes.Take(10))
            {
                sb.AppendLine($"- {recipe.Title}: {string.Join(", ", recipe.Ingredients.Take(5))}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("""
            ## Output Requirements:
                        1. Generate up to 5 recipes. Aim for 4-5 recipes when possible, but if the available ingredients are very limited it is OK to generate fewer distinct, high-quality recipes.
                            - Prioritize recipes that can be made with ONLY the available ingredients (0 missing ingredients).
                            - When possible, include at least 2 recipes that need 1-2 common ingredients (easy to obtain).
            2. For "Can Make" recipes (0 missing): ONLY use ingredients from the available list above. Basic seasonings (salt, pepper, water, cooking oil) don't count as missing.
            3. For "Missing 1-2" recipes: Can require 1-2 additional ingredients that are common and easy to obtain.
            4. For each recipe, accurately list ALL missing ingredients that are NOT in the available ingredients list.
            5. Sort recipes by how many ingredients are missing (0 missing first, then 1-2 missing).
            6. Include a match score (0-1) based on how well the recipe fits preferences.
                        7. Estimate nutrition values per serving (approximate values are fine).
            8. IMPORTANT - Set difficulty level appropriately based on these criteria:
               - "Easy": Simple techniques (boiling, basic frying), under 30 min, fewer than 6 ingredients, beginner-friendly
               - "Medium": Moderate techniques (sautéing, baking, marinating), 30-60 min, 6-10 ingredients, some cooking experience needed
               - "Hard": Advanced techniques (braising, deep-frying, complex sauces), over 60 min, many ingredients, or requires precision timing
               Vary the difficulty across recipes - don't make all recipes the same difficulty level.

            Respond with ONLY valid JSON in this exact format:
            {
              "recipes": [
                {
                  "title": "Recipe Name",
                  "description": "Brief description",
                  "servings": 4,
                  "totalTimeMinutes": 30,
                  "difficulty": "Easy",
                  "ingredients": [
                    {"name": "ingredient name", "amount": 1.0, "unit": "cup", "category": "Vegetables"}
                  ],
                  "steps": [
                    {"order": 1, "instruction": "Step description", "suggestedSeconds": 300}
                  ],
                  "missingIngredients": ["ingredient1", "ingredient2"],
                                    "matchScore": 0.85,
                                    "nutrition": {
                                        "calories": 350,
                                        "carbohydrates": 45,
                                        "fat": 12,
                                        "protein": 18,
                                        "sugar": 8,
                                        "sodium": 580,
                                        "saturatedFat": 4
                                    }
                }
              ]
            }

            Note: "difficulty" must be exactly one of: "Easy", "Medium", or "Hard" (case-sensitive).
            Note: "unit" must be one of: g, kg, ml, l, pcs, cups, tbsp, tsp.
            Note: "category" must be one of: Vegetables, Fruits, Meat, Seafood, Dairy, Grains, Spices, Beverages, Snacks, Other.
            Note: "title" must be in Title Case (e.g., "Chicken Stir Fry", not "chicken stir fry").
            """);

        return sb.ToString();
    }

    private List<GeneratedRecipe> ParseRecipeResponse(string content)
    {
        // Extract JSON from potential markdown/code fences or surrounding text
        var json = content.Trim();

        if (json.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = json.IndexOf('\n');
            json = firstNewline > 0 ? json[(firstNewline + 1)..] : json.TrimStart('`');
        }

        if (json.Contains("```"))
        {
            var endFence = json.LastIndexOf("```", StringComparison.Ordinal);
            if (endFence > 0)
            {
                json = json[..endFence].Trim();
            }
        }

        var firstBrace = json.IndexOf('{');
        var lastBrace = json.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            json = json[firstBrace..(lastBrace + 1)];
        }

        try
        {
            var response = JsonSerializer.Deserialize<AIRecipeResponse>(json, JsonOptions);
            return response?.Recipes?.Select(r => new GeneratedRecipe(
                r.Title ?? "Untitled",
                r.Description ?? "",
                r.Servings,
                r.TotalTimeMinutes,
                r.Difficulty ?? "Easy",
                r.Ingredients?.Select(i => new GeneratedIngredient(
                    i.Name ?? "",
                    i.Amount,
                    i.Unit ?? ""
                )).ToList() ?? [],
                r.Steps?.Select(s => new GeneratedStep(
                    s.Order,
                    s.Instruction ?? "",
                    s.SuggestedSeconds
                )).ToList() ?? [],
                r.MissingIngredients ?? [],
                r.MatchScore,
                r.Nutrition?.Calories,
                r.Nutrition?.Carbohydrates,
                r.Nutrition?.Fat,
                r.Nutrition?.Protein,
                r.Nutrition?.Sugar,
                r.Nutrition?.Sodium,
                r.Nutrition?.SaturatedFat
            )).ToList() ?? [];
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse AI recipe response: {Content}", json[..Math.Min(500, json.Length)]);

            var recovered = TryParsePartialRecipes(json);
            if (recovered.Count > 0)
            {
                _logger.LogWarning("Recovered {Count} recipes from partial AI response.", recovered.Count);
                return recovered;
            }

            return [];
        }
    }

    private List<GeneratedRecipe> TryParsePartialRecipes(string json)
    {
        var recipesArrayStart = json.IndexOf("\"recipes\"", StringComparison.OrdinalIgnoreCase);
        if (recipesArrayStart < 0)
            return [];

        var bracketStart = json.IndexOf('[', recipesArrayStart);
        if (bracketStart < 0)
            return [];

        var objects = ExtractJsonObjects(json, bracketStart);
        if (objects.Count == 0)
            return [];

        var recovered = new List<GeneratedRecipe>();
        foreach (var obj in objects)
        {
            try
            {
                var recipe = JsonSerializer.Deserialize<AIRecipe>(obj, JsonOptions);
                if (recipe == null)
                    continue;

                recovered.Add(new GeneratedRecipe(
                    recipe.Title ?? "Untitled",
                    recipe.Description ?? "",
                    recipe.Servings,
                    recipe.TotalTimeMinutes,
                    recipe.Difficulty ?? "Easy",
                    recipe.Ingredients?.Select(i => new GeneratedIngredient(
                        i.Name ?? "",
                        i.Amount,
                        i.Unit ?? ""
                    )).ToList() ?? [],
                    recipe.Steps?.Select(s => new GeneratedStep(
                        s.Order,
                        s.Instruction ?? "",
                        s.SuggestedSeconds
                    )).ToList() ?? [],
                    recipe.MissingIngredients ?? [],
                    recipe.MatchScore,
                    recipe.Nutrition?.Calories,
                    recipe.Nutrition?.Carbohydrates,
                    recipe.Nutrition?.Fat,
                    recipe.Nutrition?.Protein,
                    recipe.Nutrition?.Sugar,
                    recipe.Nutrition?.Sodium,
                    recipe.Nutrition?.SaturatedFat
                ));
            }
            catch (JsonException)
            {
                // Skip malformed object
            }
        }

        return recovered;
    }

    private static List<string> ExtractJsonObjects(string json, int arrayStartIndex)
    {
        var results = new List<string>();
        var inString = false;
        var escape = false;
        var depth = 0;
        var started = false;
        var startIndex = -1;

        for (var i = arrayStartIndex; i < json.Length; i++)
        {
            var ch = json[i];

            if (escape)
            {
                escape = false;
                continue;
            }

            if (ch == '\\')
            {
                escape = true;
                continue;
            }

            if (ch == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString)
                continue;

            if (ch == '{')
            {
                if (depth == 0)
                {
                    startIndex = i;
                    started = true;
                }
                depth++;
            }
            else if (ch == '}')
            {
                if (depth > 0)
                {
                    depth--;
                    if (depth == 0 && started && startIndex >= 0)
                    {
                        results.Add(json[startIndex..(i + 1)]);
                        started = false;
                        startIndex = -1;
                    }
                }
            }
            else if (ch == ']' && depth == 0)
            {
                break;
            }
        }

        return results;
    }

    // Response models
    private record OpenAIResponse(List<OpenAIChoice>? Choices);
    private record OpenAIChoice(OpenAIMessage? Message);
    private record OpenAIMessage(string? Content);

    private record AIRecipeResponse(List<AIRecipe>? Recipes);
    private record AISingleRecipeResponse(AIRecipe? Recipe);
    private record AIRecipe(
        string? Title,
        string? Description,
        decimal Servings,
        int TotalTimeMinutes,
        string? Difficulty,
        List<AIIngredient>? Ingredients,
        List<AIStep>? Steps,
        List<string>? MissingIngredients,
        decimal MatchScore,
        AINutrition? Nutrition);
    private record AIIngredient(string? Name, decimal Amount, string? Unit);
    private record AIStep(int Order, string? Instruction, int? SuggestedSeconds);
    private record AINutrition(
        decimal? Calories,
        decimal? Carbohydrates,
        decimal? Fat,
        decimal? Protein,
        decimal? Sugar,
        decimal? Sodium,
        decimal? SaturatedFat);
}
