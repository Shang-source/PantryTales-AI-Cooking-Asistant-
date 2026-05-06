using backend.Models;

namespace backend.Interfaces;

public interface INameNormalizationService
{
    /// <summary>
    /// Current algorithm version. Increment this when the normalization logic changes.
    /// </summary>
    int AlgorithmVersion { get; }

    /// <summary>
    /// Normalize a name by stripping known tokens (brands, units, promo text).
    /// </summary>
    /// <param name="name">The raw name to normalize.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Normalized name and list of removed tokens.</returns>
    Task<(string NormalizedName, List<string> RemovedTokens)> NormalizeAsync(
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Process a batch of inventory items that need normalization.
    /// Items are selected if:
    /// - normalized_name is null, OR
    /// - dictionary version is outdated, OR
    /// - algorithm version is outdated
    /// </summary>
    /// <param name="batchSize">Max items to process.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of items processed.</returns>
    Task<int> ProcessInventoryItemBatchAsync(int batchSize, CancellationToken cancellationToken = default);
}
