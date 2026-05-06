using backend.Models;

namespace backend.Interfaces;

public interface INameNormalizationRepository
{
    /// <summary>
    /// Get all tokens, optionally filtered by category and active status.
    /// </summary>
    Task<List<NameNormalizationToken>> GetTokensAsync(
        NameNormalizationTokenCategory? category = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a single token by ID.
    /// </summary>
    Task<NameNormalizationToken?> GetTokenByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a token with the given value already exists.
    /// </summary>
    Task<bool> TokenExistsAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Add a new token.
    /// </summary>
    Task<NameNormalizationToken> AddTokenAsync(NameNormalizationToken token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update an existing token.
    /// </summary>
    Task UpdateTokenAsync(NameNormalizationToken token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a token.
    /// </summary>
    Task DeleteTokenAsync(NameNormalizationToken token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk add tokens, skipping duplicates.
    /// </summary>
    Task<(int Created, int Skipped)> AddTokensAsync(IEnumerable<NameNormalizationToken> tokens, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the current dictionary and algorithm version.
    /// </summary>
    Task<NameNormalizationDictionaryVersion> GetVersionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Increment the dictionary version (called when tokens change).
    /// </summary>
    Task IncrementDictionaryVersionAsync(CancellationToken cancellationToken = default);
}
