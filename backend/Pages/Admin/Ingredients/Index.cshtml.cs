using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using backend.Data;
using backend.Interfaces;
using backend.Models;
using backend.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace backend.Pages.Admin.Ingredients;

public class IndexModel(
    AppDbContext dbContext,
    INameNormalizationRepository normalizationRepository,
    IHttpClientFactory httpClientFactory,
    IOptions<VisionOptions> visionOptions,
    ILogger<IndexModel> logger) : AdminPageModel
{
    private const int MaxPageSize = 100;

    public List<IngredientGroup> Groups { get; set; } = [];
    public List<Ingredient> AllIngredients { get; set; } = [];
    public int TotalGroups { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalGroups / (double)PageSize);

    // Summary stats
    public int PendingCount { get; set; }
    public int NeedsReviewCount { get; set; }
    public int FailedCount { get; set; }
    public int ResolvedCount { get; set; }

    [BindProperty(SupportsGet = true)]
    public IngredientResolveStatus? FilterStatus { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool ShowResolved { get; set; } = false;

    [BindProperty(SupportsGet = true, Name = "p")]
    [Range(1, int.MaxValue)]
    public int CurrentPage { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    [Range(1, MaxPageSize)]
    public int PageSize { get; set; } = 20;

    // For linking action
    [BindProperty]
    public string? LinkNormalizedName { get; set; }

    [BindProperty]
    public Guid? LinkIngredientId { get; set; }

    // For creating ingredient action
    [BindProperty]
    public string? CreateCanonicalName { get; set; }

    // For adding token action
    [BindProperty]
    public string? NewToken { get; set; }

    [BindProperty]
    public NameNormalizationTokenCategory NewTokenCategory { get; set; } = NameNormalizationTokenCategory.Brand;

    // AI suggestions
    public List<TokenSuggestion> AISuggestions { get; set; } = [];

    public async Task OnGetAsync()
    {
        PageSize = Math.Clamp(PageSize, 1, MaxPageSize);
        await LoadDataAsync();

        // Load AI suggestions from TempData if available
        if (TempData["AISuggestions"] is string suggestionsJson)
        {
            try
            {
                AISuggestions = JsonSerializer.Deserialize<List<TokenSuggestion>>(suggestionsJson) ?? [];
            }
            catch
            {
                AISuggestions = [];
            }
        }
    }

    public async Task<IActionResult> OnPostLinkAsync()
    {
        if (string.IsNullOrWhiteSpace(LinkNormalizedName) || !LinkIngredientId.HasValue)
        {
            TempData["Error"] = "Please select an ingredient to link.";
            return RedirectToPage(new { p = CurrentPage, PageSize, FilterStatus, Search, ShowResolved });
        }

        var ingredient = await dbContext.Ingredients.FindAsync(LinkIngredientId.Value);
        if (ingredient == null)
        {
            TempData["Error"] = "Ingredient not found.";
            return RedirectToPage(new { p = CurrentPage, PageSize, FilterStatus, Search, ShowResolved });
        }

        // Update all inventory items with this normalized name
        var items = await dbContext.InventoryItems
            .Where(i => i.NormalizedName == LinkNormalizedName)
            .ToListAsync();

        foreach (var item in items)
        {
            item.IngredientId = LinkIngredientId.Value;
            item.ResolveStatus = IngredientResolveStatus.Resolved;
            item.ResolveMethod = "admin_manual";
            item.ResolveConfidence = 1.0m;
            item.ResolvedAt = DateTime.UtcNow;
            item.UpdatedAt = DateTime.UtcNow;
        }

        // Also create/update alias
        var existingAlias = await dbContext.IngredientAliases
            .FirstOrDefaultAsync(a => a.NormalizedName == LinkNormalizedName);

        if (existingAlias != null)
        {
            existingAlias.IngredientId = LinkIngredientId.Value;
            existingAlias.Status = AliasResolveStatus.Resolved;
            existingAlias.ResolveMethod = "admin_manual";
            existingAlias.Confidence = 1.0m;
            existingAlias.ResolvedAt = DateTime.UtcNow;
            existingAlias.UpdatedAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync();
        TempData["Success"] = $"Linked {items.Count} items to '{ingredient.CanonicalName}'.";
        return RedirectToPage(new { p = CurrentPage, PageSize, FilterStatus, Search, ShowResolved });
    }

    public async Task<IActionResult> OnPostCreateIngredientAsync()
    {
        if (string.IsNullOrWhiteSpace(CreateCanonicalName))
        {
            TempData["Error"] = "Please provide a canonical name.";
            return RedirectToPage(new { p = CurrentPage, PageSize, FilterStatus, Search, ShowResolved });
        }

        var canonicalName = CreateCanonicalName.Trim().ToLowerInvariant();

        // Check if ingredient already exists
        var existing = await dbContext.Ingredients
            .FirstOrDefaultAsync(i => i.CanonicalName == canonicalName);

        if (existing != null)
        {
            TempData["Error"] = $"Ingredient '{canonicalName}' already exists.";
            return RedirectToPage(new { p = CurrentPage, PageSize, FilterStatus, Search, ShowResolved });
        }

        // Create new ingredient
        var ingredient = new Ingredient
        {
            CanonicalName = canonicalName
        };
        dbContext.Ingredients.Add(ingredient);

        // Link all items with this normalized name
        var items = await dbContext.InventoryItems
            .Where(i => i.NormalizedName == canonicalName)
            .ToListAsync();

        foreach (var item in items)
        {
            item.IngredientId = ingredient.Id;
            item.ResolveStatus = IngredientResolveStatus.Resolved;
            item.ResolveMethod = "admin_created";
            item.ResolveConfidence = 1.0m;
            item.ResolvedAt = DateTime.UtcNow;
            item.UpdatedAt = DateTime.UtcNow;
        }

        // Create alias
        var alias = new IngredientAlias
        {
            IngredientId = ingredient.Id,
            AliasName = canonicalName,
            NormalizedName = canonicalName,
            Status = AliasResolveStatus.Resolved,
            ResolveMethod = "admin_created",
            Confidence = 1.0m,
            ResolvedAt = DateTime.UtcNow,
            Source = "admin"
        };
        dbContext.IngredientAliases.Add(alias);

        await dbContext.SaveChangesAsync();
        TempData["Success"] = $"Created ingredient '{canonicalName}' and linked {items.Count} items.";
        return RedirectToPage(new { p = CurrentPage, PageSize, FilterStatus, Search, ShowResolved });
    }

    public async Task<IActionResult> OnPostAddTokenAsync()
    {
        if (string.IsNullOrWhiteSpace(NewToken))
        {
            TempData["Error"] = "Please provide a token.";
            return RedirectToPage(new { p = CurrentPage, PageSize, FilterStatus, Search, ShowResolved });
        }

        var token = NewToken.Trim().ToLowerInvariant();

        if (await normalizationRepository.TokenExistsAsync(token))
        {
            TempData["Error"] = $"Token '{token}' already exists.";
            return RedirectToPage(new { p = CurrentPage, PageSize, FilterStatus, Search, ShowResolved });
        }

        var newToken = new NameNormalizationToken
        {
            Token = token,
            Category = NewTokenCategory,
            IsActive = true,
            IsRegex = false
        };

        await normalizationRepository.AddTokenAsync(newToken);
        TempData["Success"] = $"Added token '{token}' to {NewTokenCategory} category. Items will be re-normalized on next background run.";
        return RedirectToPage(new { p = CurrentPage, PageSize, FilterStatus, Search, ShowResolved });
    }

    public async Task<IActionResult> OnPostSuggestTokensAsync()
    {
        try
        {
            // Get sample raw ingredient names that haven't been fully resolved
            var sampleNames = await dbContext.InventoryItems
                .AsNoTracking()
                .Where(i => i.ResolveStatus != IngredientResolveStatus.Resolved)
                .OrderByDescending(i => i.CreatedAt)
                .Select(i => i.Name)
                .Distinct()
                .Take(50)
                .ToListAsync();

            if (sampleNames.Count == 0)
            {
                TempData["Error"] = "No unresolved ingredient names to analyze.";
                return RedirectToPage(new { p = CurrentPage, PageSize, FilterStatus, Search, ShowResolved });
            }

            // Get existing tokens to avoid suggesting duplicates
            var existingTokens = await normalizationRepository.GetTokensAsync(null, true);
            var existingSet = existingTokens.Select(t => t.Token.ToLowerInvariant()).ToHashSet();

            var suggestions = await GetAISuggestionsAsync(sampleNames, existingSet);

            if (suggestions.Count > 0)
            {
                TempData["AISuggestions"] = JsonSerializer.Serialize(suggestions);
                TempData["Success"] = $"AI suggested {suggestions.Count} potential tokens to add.";
            }
            else
            {
                TempData["Info"] = "AI didn't find any new tokens to suggest.";
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get AI token suggestions");
            TempData["Error"] = $"AI suggestion failed: {ex.Message}";
        }

        return RedirectToPage(new { p = CurrentPage, PageSize, FilterStatus, Search, ShowResolved });
    }

    private async Task<List<TokenSuggestion>> GetAISuggestionsAsync(List<string> sampleNames, HashSet<string> existingTokens)
    {
        var options = visionOptions.Value;
        if (string.IsNullOrEmpty(options.ApiKey))
        {
            throw new InvalidOperationException("OpenAI API key not configured");
        }

        var prompt = BuildTokenSuggestionPrompt(sampleNames, existingTokens);

        var requestBody = new
        {
            model = options.Model ?? "gpt-4o",
            messages = new[]
            {
                new
                {
                    role = "system",
                    content = """
                        You are an expert at analyzing ingredient names and identifying noise words that should be stripped
                        for normalization. Your job is to identify brands, units, packaging terms, promotional words,
                        and other noise that prevents matching ingredients to their canonical names.
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
            max_tokens = 1000,
            temperature = 0.3
        };

        using var httpClient = httpClientFactory.CreateClient();
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        httpRequest.Headers.Add("Authorization", $"Bearer {options.ApiKey}");
        httpRequest.Content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        var response = await httpClient.SendAsync(httpRequest);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"OpenAI API error: {response.StatusCode}");
        }

        var openAiResponse = JsonSerializer.Deserialize<OpenAIResponse>(responseContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        var content = openAiResponse?.Choices?.FirstOrDefault()?.Message?.Content;
        if (string.IsNullOrEmpty(content))
        {
            return [];
        }

        return ParseTokenSuggestions(content, existingTokens);
    }

    private static string BuildTokenSuggestionPrompt(List<string> sampleNames, HashSet<string> existingTokens)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Analyze these ingredient names and suggest words/patterns to strip for normalization:");
        sb.AppendLine();
        sb.AppendLine("## Sample Ingredient Names:");
        foreach (var name in sampleNames.Take(30))
        {
            sb.AppendLine($"- {name}");
        }
        sb.AppendLine();

        if (existingTokens.Count > 0)
        {
            sb.AppendLine("## Already Configured Tokens (do NOT suggest these):");
            foreach (var token in existingTokens.Take(50))
            {
                sb.AppendLine($"- {token}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("""
            ## Task:
            Identify words that should be stripped from ingredient names. Look for:
            1. Brand names (supermarket brands, product brands)
            2. Units and sizes (500ml, 1kg, 250g, etc.)
            3. Packaging terms (jar, can, bottle, pack, bag)
            4. Promotional words (special, sale, new, premium, fresh)
            5. Generic noise (organic, natural, etc. - unless they're meaningful)

            ## Output Format:
            Return JSON with suggested tokens:
            {
              "suggestions": [
                {
                  "token": "the word to strip",
                  "category": "Brand|Unit|Packaging|Promo|Noise",
                  "reason": "Brief explanation",
                  "examples": ["example ingredient that contains this"]
                }
              ]
            }

            Only suggest tokens that appear in the sample names provided.
            Do NOT suggest tokens that are already in the configured list.
            Suggest 5-15 tokens maximum, prioritizing the most impactful ones.
            """);

        return sb.ToString();
    }

    private static List<TokenSuggestion> ParseTokenSuggestions(string content, HashSet<string> existingTokens)
    {
        try
        {
            var json = content.Trim();
            if (json.StartsWith("```"))
            {
                var firstNewline = json.IndexOf('\n');
                json = firstNewline > 0 ? json[(firstNewline + 1)..] : json.TrimStart('`');
            }
            if (json.Contains("```"))
            {
                var endFence = json.LastIndexOf("```");
                if (endFence > 0) json = json[..endFence].Trim();
            }

            var response = JsonSerializer.Deserialize<AISuggestionResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return response?.Suggestions?
                .Where(s => !string.IsNullOrWhiteSpace(s.Token) &&
                           !existingTokens.Contains(s.Token.ToLowerInvariant()))
                .Select(s => new TokenSuggestion
                {
                    Token = s.Token!.Trim().ToLowerInvariant(),
                    Category = ParseCategory(s.Category),
                    Reason = s.Reason ?? "",
                    Examples = s.Examples ?? []
                })
                .DistinctBy(s => s.Token)
                .Take(15)
                .ToList() ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static NameNormalizationTokenCategory ParseCategory(string? category)
    {
        return category?.ToLowerInvariant() switch
        {
            "brand" => NameNormalizationTokenCategory.Brand,
            "unit" => NameNormalizationTokenCategory.Unit,
            "packaging" => NameNormalizationTokenCategory.Packaging,
            "promo" => NameNormalizationTokenCategory.Promo,
            _ => NameNormalizationTokenCategory.Noise
        };
    }

    private async Task LoadDataAsync()
    {
        // Get summary counts
        PendingCount = await dbContext.InventoryItems.CountAsync(i => i.ResolveStatus == IngredientResolveStatus.Pending);
        NeedsReviewCount = await dbContext.InventoryItems.CountAsync(i => i.ResolveStatus == IngredientResolveStatus.NeedsReview);
        FailedCount = await dbContext.InventoryItems.CountAsync(i => i.ResolveStatus == IngredientResolveStatus.Failed);
        ResolvedCount = await dbContext.InventoryItems.CountAsync(i => i.ResolveStatus == IngredientResolveStatus.Resolved);

        // Load all ingredients for linking dropdown
        AllIngredients = await dbContext.Ingredients
            .OrderBy(i => i.CanonicalName)
            .ToListAsync();

        // Build query for groups
        var query = dbContext.InventoryItems
            .AsNoTracking()
            .Where(i => i.NormalizedName != null);

        // Apply status filter
        if (FilterStatus.HasValue)
        {
            query = query.Where(i => i.ResolveStatus == FilterStatus.Value);
        }
        else if (!ShowResolved)
        {
            // By default, hide resolved items
            query = query.Where(i => i.ResolveStatus != IngredientResolveStatus.Resolved);
        }

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(Search))
        {
            var search = $"%{Search.Trim()}%";
            query = query.Where(i =>
                EF.Functions.ILike(i.NormalizedName!, search) ||
                EF.Functions.ILike(i.Name, search));
        }

        // Group by normalized name and get stats
        var groupedQuery = query
            .GroupBy(i => new { i.NormalizedName, i.IngredientId })
            .Select(g => new
            {
                NormalizedName = g.Key.NormalizedName,
                IngredientId = g.Key.IngredientId,
                Count = g.Count(),
                PendingCount = g.Count(i => i.ResolveStatus == IngredientResolveStatus.Pending),
                ResolvedCount = g.Count(i => i.ResolveStatus == IngredientResolveStatus.Resolved),
                NeedsReviewCount = g.Count(i => i.ResolveStatus == IngredientResolveStatus.NeedsReview),
                FailedCount = g.Count(i => i.ResolveStatus == IngredientResolveStatus.Failed)
            });

        TotalGroups = await groupedQuery.CountAsync();

        // Ensure page is within valid range
        if (CurrentPage > TotalPages && TotalPages > 0)
        {
            CurrentPage = TotalPages;
        }

        var groupSummaries = await groupedQuery
            .OrderByDescending(g => g.Count)
            .Skip((CurrentPage - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        // For each group, get sample items
        Groups = [];
        foreach (var summary in groupSummaries)
        {
            var sampleItems = await dbContext.InventoryItems
                .AsNoTracking()
                .Include(i => i.Ingredient)
                .Where(i => i.NormalizedName == summary.NormalizedName)
                .OrderByDescending(i => i.CreatedAt)
                .Take(5)
                .ToListAsync();

            var removedTokens = new HashSet<string>();
            foreach (var item in sampleItems)
            {
                if (!string.IsNullOrEmpty(item.NameNormalizationRemovedTokens))
                {
                    try
                    {
                        var tokens = JsonSerializer.Deserialize<List<string>>(item.NameNormalizationRemovedTokens);
                        if (tokens != null)
                        {
                            foreach (var t in tokens)
                            {
                                removedTokens.Add(t);
                            }
                        }
                    }
                    catch
                    {
                        // Ignore deserialization errors
                    }
                }
            }

            Groups.Add(new IngredientGroup
            {
                NormalizedName = summary.NormalizedName ?? "(empty)",
                IngredientId = summary.IngredientId,
                IngredientCanonicalName = sampleItems.FirstOrDefault()?.Ingredient?.CanonicalName,
                TotalCount = summary.Count,
                PendingCount = summary.PendingCount,
                ResolvedCount = summary.ResolvedCount,
                NeedsReviewCount = summary.NeedsReviewCount,
                FailedCount = summary.FailedCount,
                SampleRawNames = sampleItems.Select(i => i.Name).Distinct().Take(5).ToList(),
                RemovedTokens = removedTokens.OrderBy(t => t).ToList()
            });
        }
    }

    public class IngredientGroup
    {
        public string NormalizedName { get; set; } = string.Empty;
        public Guid? IngredientId { get; set; }
        public string? IngredientCanonicalName { get; set; }
        public int TotalCount { get; set; }
        public int PendingCount { get; set; }
        public int ResolvedCount { get; set; }
        public int NeedsReviewCount { get; set; }
        public int FailedCount { get; set; }
        public List<string> SampleRawNames { get; set; } = [];
        public List<string> RemovedTokens { get; set; } = [];
    }

    public class TokenSuggestion
    {
        public string Token { get; set; } = string.Empty;
        public NameNormalizationTokenCategory Category { get; set; }
        public string Reason { get; set; } = string.Empty;
        public List<string> Examples { get; set; } = [];
    }

    // OpenAI response models
    private record OpenAIResponse(List<OpenAIChoice>? Choices);
    private record OpenAIChoice(OpenAIMessage? Message);
    private record OpenAIMessage(string? Content);
    private record AISuggestionResponse(List<AISuggestion>? Suggestions);
    private record AISuggestion(string? Token, string? Category, string? Reason, List<string>? Examples);
}
