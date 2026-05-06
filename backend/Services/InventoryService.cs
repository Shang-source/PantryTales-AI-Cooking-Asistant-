using backend.Interfaces;
using backend.Models;
using backend.Dtos.Inventory;
using backend.Extensions;
using backend.Data;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public class InventoryService(
    IHouseholdService householdService,
    IInventoryRepository inventoryRepository,
    ISmartRecipeService smartRecipeService,
    AppDbContext dbContext,
    ILogger<InventoryService> logger) : IInventoryService
{

    private async Task<InventoryResult<InventoryItem>> GetAuthorizedItemAsync(
        Guid itemId,
        string clerkUserId,
        CancellationToken cancellationToken)
    {
        var item = await inventoryRepository.GetByIdAsync(itemId, cancellationToken);
        if (item is null)
        {
            logger.LogInformation("Inventory item {ItemId} not found.", itemId);
            return InventoryResult<InventoryItem>.Fail(InventoryError.ItemNotFound);
        }

        var activeHouseholdId = await householdService.GetActiveHouseholdIdAsync(clerkUserId, cancellationToken);
        if (activeHouseholdId is null)
        {
            logger.LogWarning("No active household for user {ClerkUserId} when accessing item {ItemId}.", clerkUserId, itemId);
            return InventoryResult<InventoryItem>.Fail(InventoryError.NoActiveHousehold);
        }

        if (item.HouseholdId != activeHouseholdId.Value)
        {
            logger.LogWarning("User {ClerkUserId} cannot access inventory item {ItemId}.", clerkUserId, itemId);
            return InventoryResult<InventoryItem>.Fail(InventoryError.HouseholdMismatch);
        }

        return InventoryResult<InventoryItem>.Ok(item);
    }

    public async Task<InventoryResult<InventoryItemResponseDto>> CreateInventoryItemAsync(
        string clerkUserId,
        CreateInventoryItemRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var activeHouseholdId = await householdService.GetActiveHouseholdIdAsync(clerkUserId, cancellationToken);
        if (activeHouseholdId is null)
        {
            logger.LogWarning("No active household for user {ClerkUserId} when creating item.", clerkUserId);
            return InventoryResult<InventoryItemResponseDto>.Fail(InventoryError.NoActiveHousehold);
        }
        var now = DateTime.UtcNow;
        var createItem = request.ToEntity(activeHouseholdId.Value, now);

        await inventoryRepository.AddAsync(createItem, cancellationToken);

        // Invalidate smart recipes when inventory changes
        _ = smartRecipeService.InvalidateForHouseholdAsync(activeHouseholdId.Value, cancellationToken);

        logger.LogInformation("Created inventory item {ItemId}.", createItem.Id);
        return InventoryResult<InventoryItemResponseDto>.Ok(createItem.ToResponseDto());
    }

    public async Task<InventoryResult<List<InventoryItemResponseDto>>> CreateBatchAsync(
        string clerkUserId,
        IReadOnlyList<CreateInventoryItemRequestDto> items,
        CancellationToken cancellationToken)
    {
        var activeHouseholdId = await householdService.GetActiveHouseholdIdAsync(clerkUserId, cancellationToken);
        if (activeHouseholdId is null)
        {
            logger.LogWarning("No active household for user {ClerkUserId} when creating items.", clerkUserId);
            return InventoryResult<List<InventoryItemResponseDto>>.Fail(InventoryError.NoActiveHousehold);
        }

        var now = DateTime.UtcNow;

        var inventoryItems = items
            .Select(dto => dto.ToEntity(activeHouseholdId.Value, now))
            .ToList();

        await inventoryRepository.AddRangeAsync(inventoryItems, cancellationToken);
        var result = inventoryItems
            .Select(i => i.ToResponseDto())
            .ToList();

        // Invalidate smart recipes when inventory changes
        _ = smartRecipeService.InvalidateForHouseholdAsync(activeHouseholdId.Value, cancellationToken);

        logger.LogInformation("Created {ItemCount} inventory items.", inventoryItems.Count);
        return InventoryResult<List<InventoryItemResponseDto>>.Ok(result);
    }

    public async Task<InventoryResult<InventoryListResponseDto>> GetInventoryListAsync(
        string clerkUserId,
        InventoryListRequestDto query,
        CancellationToken cancellationToken)
    {
        var activeHouseholdId = await householdService.GetActiveHouseholdIdAsync(clerkUserId, cancellationToken); 
        if (activeHouseholdId is null)
        {
            logger.LogWarning("No active household for user {ClerkUserId} when when retrieving inventory list.", clerkUserId);
            return InventoryResult<InventoryListResponseDto>.Fail(InventoryError.NoActiveHousehold);
        }

        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var (items, totalCount) =
            await inventoryRepository.QueryAsync(
                activeHouseholdId.Value,
                query,
                page,
                pageSize,
                cancellationToken);

        var dtoItems = items
            .Select(i => i.ToResponseDto())
            .ToList();

        return InventoryResult<InventoryListResponseDto>.Ok(new InventoryListResponseDto(dtoItems, totalCount, page, pageSize));
    }

    public async Task<InventoryResult<InventoryStatsResponseDto>> GetInventoryStatsAsync(
        string clerkUserId,
        CancellationToken cancellationToken = default)
    {
        var activeHouseholdId = await householdService.GetActiveHouseholdIdAsync(clerkUserId, cancellationToken);
        if (activeHouseholdId is null)
        {
            logger.LogWarning("No active household for user {ClerkUserId} when requesting inventory stats.", clerkUserId);
            return InventoryResult<InventoryStatsResponseDto>.Fail(InventoryError.NoActiveHousehold);
        }

        var stats = await inventoryRepository.GetStatsAsync(activeHouseholdId.Value, cancellationToken);

        logger.LogInformation("Inventory stats retrieved successfully for household {HouseholdId}.", activeHouseholdId.Value);
        return InventoryResult<InventoryStatsResponseDto>.Ok(stats);
    }

    public async Task<InventoryResult<InventoryItemResponseDto>> UpdateInventoryItemAsync(
        Guid itemId,
        string clerkUserId,
        UpdateInventoryItemRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var authResult = await GetAuthorizedItemAsync(itemId, clerkUserId, cancellationToken);
        if (!authResult.IsSuccess)
        {
            return InventoryResult<InventoryItemResponseDto>.Fail(authResult.Result);
        }
        var existing = authResult.Data!;

        existing.Unit = request.Unit;
        existing.Amount = request.Amount;
        existing.StorageMethod = request.StorageMethod;
        existing.ExpirationDate = request.ExpirationDays.HasValue
            ? DateOnly.FromDateTime(DateTime.UtcNow)
                .AddDays(request.ExpirationDays.Value)
            : null;
        existing.UpdatedAt = DateTime.UtcNow;

        await inventoryRepository.UpdateAsync(existing, cancellationToken);

        // Invalidate smart recipes when inventory changes
        _ = smartRecipeService.InvalidateForHouseholdAsync(existing.HouseholdId, cancellationToken);

        logger.LogInformation("Updated inventory item {ItemId}.", existing.Id);
        return InventoryResult<InventoryItemResponseDto>.Ok(existing.ToResponseDto());
    }

    public async Task<InventoryActionResult> DeleteInventoryItemAsync(
        Guid itemId,
        string clerkUserId, 
        CancellationToken cancellationToken = default)
    {

        var authResult = await GetAuthorizedItemAsync(itemId, clerkUserId, cancellationToken);
        if (!authResult.IsSuccess)
        {
            return InventoryActionResult.Fail(authResult.Result);
        }

        var deleted = await inventoryRepository.DeleteAsync(itemId, cancellationToken);
        if (!deleted)
            return InventoryActionResult.Fail(InventoryError.ItemNotFound);

        // Invalidate smart recipes when inventory changes
        _ = smartRecipeService.InvalidateForHouseholdAsync(authResult.Data!.HouseholdId, cancellationToken);

        return InventoryActionResult.Ok();
    }

    public async Task<InventoryResult<DeductionResult>> DeductForRecipeAsync(
        string clerkUserId,
        Guid recipeId,
        int servings,
        CancellationToken cancellationToken = default)
    {
        var activeHouseholdId = await householdService.GetActiveHouseholdIdAsync(clerkUserId, cancellationToken);
        if (activeHouseholdId is null)
        {
            logger.LogWarning("No active household for user {ClerkUserId} when deducting for recipe.", clerkUserId);
            return InventoryResult<DeductionResult>.Fail(InventoryError.NoActiveHousehold);
        }

        // Fetch recipe with ingredients
        var recipe = await dbContext.Recipes
            .Include(r => r.Ingredients)
                .ThenInclude(ri => ri.Ingredient)
            .FirstOrDefaultAsync(r => r.Id == recipeId, cancellationToken);

        if (recipe == null)
        {
            logger.LogWarning("Recipe {RecipeId} not found for inventory deduction.", recipeId);
            return InventoryResult<DeductionResult>.Fail(InventoryError.IngredientNotFound);
        }

        // Get all active inventory items for the household
        var inventoryItems = await dbContext.InventoryItems
            .Where(i => i.HouseholdId == activeHouseholdId.Value && i.Status == InventoryItemStatus.Active)
            .ToListAsync(cancellationToken);

        // Calculate servings multiplier
        var recipeServings = recipe.Servings ?? 1m;
        var servingsMultiplier = servings / recipeServings;

        var deductionResults = new List<DeductionItemResult>();
        var itemsToUpdate = new List<InventoryItem>();
        var itemsToDelete = new List<InventoryItem>();

        foreach (var recipeIngredient in recipe.Ingredients)
        {
            var ingredientName = recipeIngredient.Ingredient?.CanonicalName ?? "Unknown";
            var requiredAmount = recipeIngredient.Amount * servingsMultiplier;

            // Skip if no amount required
            if (requiredAmount <= 0)
            {
                deductionResults.Add(new DeductionItemResult(
                    ingredientName,
                    null,
                    requiredAmount,
                    0,
                    recipeIngredient.Unit,
                    DeductionStatus.NotFound
                ));
                continue;
            }

            // Find best matching inventory item using fuzzy matching
            var matchedItem = FindBestMatch(ingredientName, inventoryItems);

            if (matchedItem == null)
            {
                deductionResults.Add(new DeductionItemResult(
                    ingredientName,
                    null,
                    requiredAmount,
                    0,
                    recipeIngredient.Unit,
                    DeductionStatus.NotFound
                ));
                continue;
            }

            // Calculate how much we can deduct
            var availableAmount = matchedItem.Amount;
            var deductAmount = Math.Min(availableAmount, requiredAmount);

            var status = deductAmount >= requiredAmount
                ? DeductionStatus.FullyDeducted
                : DeductionStatus.PartiallyDeducted;

            // Update inventory item
            matchedItem.Amount -= deductAmount;
            matchedItem.UpdatedAt = DateTime.UtcNow;

            if (matchedItem.Amount <= 0)
            {
                // Remove item from inventory
                matchedItem.Status = InventoryItemStatus.Consumed;
                status = DeductionStatus.RemovedFromInventory;
                itemsToDelete.Add(matchedItem);
            }
            else
            {
                itemsToUpdate.Add(matchedItem);
            }

            deductionResults.Add(new DeductionItemResult(
                ingredientName,
                matchedItem.Name,
                requiredAmount,
                deductAmount,
                recipeIngredient.Unit,
                status
            ));
        }

        // Save changes
        if (itemsToUpdate.Count > 0 || itemsToDelete.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);

            // Invalidate smart recipes when inventory changes
            _ = smartRecipeService.InvalidateForHouseholdAsync(activeHouseholdId.Value, cancellationToken);
        }

        var result = new DeductionResult(
            TotalDeducted: deductionResults.Count(r => r.Status != DeductionStatus.NotFound),
            ItemsFullyDeducted: deductionResults.Count(r => r.Status == DeductionStatus.FullyDeducted),
            ItemsPartiallyDeducted: deductionResults.Count(r => r.Status == DeductionStatus.PartiallyDeducted),
            ItemsNotFound: deductionResults.Count(r => r.Status == DeductionStatus.NotFound),
            Items: deductionResults
        );

        logger.LogInformation(
            "Deducted inventory for recipe {RecipeId}: {TotalDeducted} items deducted, {NotFound} not found.",
            recipeId, result.TotalDeducted, result.ItemsNotFound);

        return InventoryResult<DeductionResult>.Ok(result);
    }

    /// <summary>
    /// Find the best matching inventory item for an ingredient using fuzzy matching.
    /// </summary>
    private static InventoryItem? FindBestMatch(string ingredientName, List<InventoryItem> inventoryItems)
    {
        if (string.IsNullOrWhiteSpace(ingredientName) || inventoryItems.Count == 0)
            return null;

        var normalizedIngredient = NormalizeForMatching(ingredientName);
        InventoryItem? bestMatch = null;
        double bestScore = 0.0;
        const double threshold = 0.4; // Minimum similarity score to consider a match

        foreach (var item in inventoryItems)
        {
            var normalizedItem = NormalizeForMatching(item.Name);

            // Calculate similarity score
            var score = CalculateSimilarity(normalizedIngredient, normalizedItem);

            if (score > bestScore && score >= threshold)
            {
                bestScore = score;
                bestMatch = item;
            }
        }

        return bestMatch;
    }

    /// <summary>
    /// Normalize ingredient name for matching (lowercase, trim, remove common prefixes).
    /// </summary>
    private static string NormalizeForMatching(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var normalized = name.ToLowerInvariant().Trim();

        // Remove common prefixes
        var prefixes = new[] { "fresh ", "dried ", "frozen ", "canned ", "chopped ", "minced ", "sliced ", "diced ", "ground " };
        var matchedPrefix = prefixes.FirstOrDefault(p => normalized.StartsWith(p, StringComparison.OrdinalIgnoreCase));
        if (matchedPrefix != null)
        {
            normalized = normalized[matchedPrefix.Length..];
        }

        // Remove plurals (simple s/es removal)
        if (normalized.EndsWith("ies"))
        {
            normalized = normalized[..^3] + "y";
        }
        else if (normalized.EndsWith("es") && normalized.Length > 3)
        {
            normalized = normalized[..^2];
        }
        else if (normalized.EndsWith("s") && normalized.Length > 2)
        {
            normalized = normalized[..^1];
        }

        return normalized.Trim();
    }

    /// <summary>
    /// Calculate similarity between two strings using a combination of methods.
    /// Returns a score between 0 and 1.
    /// </summary>
    private static double CalculateSimilarity(string a, string b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
            return 0.0;

        // Exact match
        if (a.Equals(b, StringComparison.OrdinalIgnoreCase))
            return 1.0;

        // Substring containment (high score for partial matches)
        if (a.Contains(b, StringComparison.OrdinalIgnoreCase) ||
            b.Contains(a, StringComparison.OrdinalIgnoreCase))
            return 0.85;

        // Word overlap check
        var wordsA = a.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var wordsB = b.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var commonWords = wordsA.Count(wa =>
            wordsB.Any(wb =>
                wa.Equals(wb, StringComparison.OrdinalIgnoreCase) ||
                wa.Contains(wb, StringComparison.OrdinalIgnoreCase) ||
                wb.Contains(wa, StringComparison.OrdinalIgnoreCase)));

        if (commonWords > 0)
        {
            var wordOverlapScore = (double)commonWords / Math.Max(wordsA.Length, wordsB.Length);
            if (wordOverlapScore >= 0.5)
                return 0.7 + (wordOverlapScore * 0.2);
        }

        // Levenshtein distance for fuzzy matching
        var maxLen = Math.Max(a.Length, b.Length);
        if (maxLen == 0) return 1.0;

        var distance = LevenshteinDistance(a, b);
        return 1.0 - ((double)distance / maxLen);
    }

    /// <summary>
    /// Calculate Levenshtein distance between two strings.
    /// </summary>
    private static int LevenshteinDistance(string a, string b)
    {
        if (string.IsNullOrEmpty(a)) return b?.Length ?? 0;
        if (string.IsNullOrEmpty(b)) return a.Length;

        var lengthA = a.Length;
        var lengthB = b.Length;
        var distances = new int[lengthA + 1, lengthB + 1];

        for (var i = 0; i <= lengthA; i++)
            distances[i, 0] = i;
        for (var j = 0; j <= lengthB; j++)
            distances[0, j] = j;

        for (var i = 1; i <= lengthA; i++)
        {
            for (var j = 1; j <= lengthB; j++)
            {
                var cost = char.ToLowerInvariant(a[i - 1]) == char.ToLowerInvariant(b[j - 1]) ? 0 : 1;
                distances[i, j] = Math.Min(
                    Math.Min(distances[i - 1, j] + 1, distances[i, j - 1] + 1),
                    distances[i - 1, j - 1] + cost);
            }
        }

        return distances[lengthA, lengthB];
    }
}
