using System.Text.Json;
using System.Text.RegularExpressions;
using backend.Data;
using backend.Interfaces;
using backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace backend.Services;

public partial class NameNormalizationService(
    AppDbContext context,
    INameNormalizationRepository repository,
    IMemoryCache cache,
    ILogger<NameNormalizationService> logger) : INameNormalizationService
{
    private const string TokensCacheKey = "normalization_tokens";
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Increment this when the normalization algorithm logic changes.
    /// </summary>
    public int AlgorithmVersion => 1;

    public async Task<(string NormalizedName, List<string> RemovedTokens)> NormalizeAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return (string.Empty, []);

        var tokens = await GetActiveTokensAsync(cancellationToken);
        var normalized = name.Trim();
        var removed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var token in tokens.Where(token => token.IsActive))
        {
            const RegexOptions options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

            // Use the token as a regex pattern directly
            var pattern = token.IsRegex
                ? token.Token
                :
                // Exact word match with word boundaries
                $@"\b{Regex.Escape(token.Token)}\b";

            try
            {
                var regex = new Regex(pattern, options, TimeSpan.FromMilliseconds(100));
                var matches = regex.Matches(normalized);

                if (matches.Count > 0)
                {
                    foreach (var matchValue in matches.Cast<Match>()
                                 .Select(m => m.Value)
                                 .Where(v => !removed.Contains(v)))
                    {
                        removed.Add(matchValue);
                    }

                    normalized = regex.Replace(normalized, " ");
                }
            }
            catch (RegexMatchTimeoutException)
            {
                logger.LogWarning("Regex timeout for token {TokenId}: '{Token}'", token.Id, token.Token);
            }
            catch (ArgumentException ex)
            {
                logger.LogWarning(ex, "Invalid regex pattern for token {TokenId}: '{Token}'", token.Id, token.Token);
            }
        }

        // Clean up: collapse multiple spaces, trim
        normalized = MyRegex().Replace(normalized, " ").Trim();

        return (normalized, removed.ToList());
    }

    public async Task<int> ProcessInventoryItemBatchAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var version = await repository.GetVersionAsync(cancellationToken);

        // Find items that need normalization OR resolution
        var items = await context.InventoryItems
            .Where(i => i.NormalizedName == null ||
                        i.NameNormalizationDictionaryVersion == null ||
                        i.NameNormalizationDictionaryVersion < version.DictionaryVersion ||
                        i.NameNormalizationAlgorithmVersion == null ||
                        i.NameNormalizationAlgorithmVersion < version.AlgorithmVersion ||
                        (i.IngredientId == null && i.ResolveStatus == IngredientResolveStatus.Pending))
            .OrderBy(i => i.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (items.Count == 0)
            return 0;

        var processed = 0;

        foreach (var item in items)
        {
            try
            {
                // Step 1: Normalize the name if needed
                var needsNormalization = item.NormalizedName == null ||
                                         item.NameNormalizationDictionaryVersion < version.DictionaryVersion ||
                                         item.NameNormalizationAlgorithmVersion < AlgorithmVersion;

                if (needsNormalization)
                {
                    try
                    {
                        var (normalizedName, removedTokens) = await NormalizeAsync(item.Name ?? string.Empty, cancellationToken);

                        item.NormalizedName = normalizedName;
                        item.NameNormalizationDictionaryVersion = version.DictionaryVersion;
                        item.NameNormalizationAlgorithmVersion = AlgorithmVersion;
                        item.NameNormalizationRemovedTokens = removedTokens.Count > 0
                            ? JsonSerializer.Serialize(removedTokens)
                            : null;
                    }
                    catch (Exception ex)
                    {
                        // Log normalization error but continue - item can still be processed
                        logger.LogWarning(ex, "Failed to normalize inventory item {ItemId}: '{Name}'", item.Id, item.Name);
                        // Set normalized name to original name so item isn't repeatedly retried
                        item.NormalizedName = item.Name?.ToLowerInvariant().Trim();
                        item.NameNormalizationDictionaryVersion = version.DictionaryVersion;
                        item.NameNormalizationAlgorithmVersion = AlgorithmVersion;
                    }
                }

                // Step 2: Resolve ingredient if not already resolved
                if (item.IngredientId == null && item.ResolveStatus == IngredientResolveStatus.Pending)
                {
                    await ResolveIngredientAsync(item, cancellationToken);
                }

                item.UpdatedAt = DateTime.UtcNow;
                processed++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to resolve ingredient for item {ItemId}: '{Name}'", item.Id, item.Name);
                item.ResolveStatus = IngredientResolveStatus.Failed;
                item.LastResolveError = ex.Message;
                item.ResolveAttempts++;
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Processed {Processed}/{Total} inventory items.", processed, items.Count);

        return processed;
    }

    /// <summary>
    /// Resolve ingredient for an inventory item by finding or creating matching alias/ingredient.
    /// </summary>
    private async Task ResolveIngredientAsync(InventoryItem item, CancellationToken cancellationToken)
    {
        var normalizedName = item.NormalizedName?.ToLowerInvariant().Trim();
        if (string.IsNullOrEmpty(normalizedName))
        {
            item.ResolveStatus = IngredientResolveStatus.NeedsReview;
            item.LastResolveError = "Normalized name is empty";
            return;
        }

        item.ResolveAttempts++;

        // Step 2a: Check if there's an existing alias with this normalized name that has an ingredient
        var existingAlias = await context.IngredientAliases
            .Where(a => a.NormalizedName != null &&
                        a.NormalizedName.Equals(normalizedName, StringComparison.OrdinalIgnoreCase) &&
                        a.IngredientId != null &&
                        a.Status == AliasResolveStatus.Resolved)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingAlias != null)
        {
            // Found existing resolved alias - bind to same ingredient
            item.IngredientId = existingAlias.IngredientId;
            item.ResolveStatus = IngredientResolveStatus.Resolved;
            item.ResolveConfidence = 1.0m;
            item.ResolveMethod = "alias_match";
            item.ResolvedAt = DateTime.UtcNow;

            logger.LogDebug("Resolved item '{Name}' to ingredient {IngredientId} via alias match.",
                item.Name, existingAlias.IngredientId);
            return;
        }

        // Step 2b: Check if there's an ingredient with this canonical name
        var existingIngredient = await context.Ingredients
            .Where(i => i.CanonicalName.ToLowerInvariant() == normalizedName)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingIngredient != null)
        {
            // Found existing ingredient - create alias as cache and bind
            var alias = new IngredientAlias
            {
                IngredientId = existingIngredient.Id,
                AliasName = item.Name,
                NormalizedName = normalizedName,
                NameNormalizationDictionaryVersion = item.NameNormalizationDictionaryVersion,
                NameNormalizationAlgorithmVersion = item.NameNormalizationAlgorithmVersion,
                NameNormalizationRemovedTokens = item.NameNormalizationRemovedTokens,
                Source = "inventory_resolution",
                Confidence = 1.0m,
                Status = AliasResolveStatus.Resolved,
                ResolveMethod = "canonical_match",
                ResolvedAt = DateTime.UtcNow
            };

            context.IngredientAliases.Add(alias);

            item.IngredientId = existingIngredient.Id;
            item.ResolveStatus = IngredientResolveStatus.Resolved;
            item.ResolveConfidence = 1.0m;
            item.ResolveMethod = "canonical_match";
            item.ResolvedAt = DateTime.UtcNow;

            logger.LogDebug("Resolved item '{Name}' to ingredient {IngredientId} via canonical name match.",
                item.Name, existingIngredient.Id);
            return;
        }

        // Step 2c: No match found - create new ingredient and alias
        var newIngredient = new Ingredient
        {
            CanonicalName = normalizedName
        };

        context.Ingredients.Add(newIngredient);

        var newAlias = new IngredientAlias
        {
            IngredientId = newIngredient.Id,
            AliasName = item.Name,
            NormalizedName = normalizedName,
            NameNormalizationDictionaryVersion = item.NameNormalizationDictionaryVersion,
            NameNormalizationAlgorithmVersion = item.NameNormalizationAlgorithmVersion,
            NameNormalizationRemovedTokens = item.NameNormalizationRemovedTokens,
            Source = "inventory_creation",
            Confidence = 1.0m,
            Status = AliasResolveStatus.Resolved,
            ResolveMethod = "auto_created",
            ResolvedAt = DateTime.UtcNow
        };

        context.IngredientAliases.Add(newAlias);

        item.IngredientId = newIngredient.Id;
        item.ResolveStatus = IngredientResolveStatus.Resolved;
        item.ResolveConfidence = 1.0m;
        item.ResolveMethod = "auto_created";
        item.ResolvedAt = DateTime.UtcNow;

        logger.LogInformation("Created new ingredient '{CanonicalName}' for item '{Name}'.",
            normalizedName, item.Name);
    }

    private async Task<List<NameNormalizationToken>> GetActiveTokensAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(TokensCacheKey, out List<NameNormalizationToken>? tokens) && tokens is not null)
            return tokens;

        tokens = await repository.GetTokensAsync(isActive: true, cancellationToken: cancellationToken);

        cache.Set(TokensCacheKey, tokens, CacheExpiry);

        return tokens;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex MyRegex();
}