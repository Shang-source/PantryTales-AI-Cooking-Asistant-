namespace backend.Interfaces;

/// <summary>
/// Provider-agnostic interface for text embedding generation.
/// Implementations can use OpenAI, Gemini, Voyage, or other providers.
/// </summary>
public interface IEmbeddingProvider
{
    /// <summary>
    /// Name of the provider (e.g., "Gemini", "OpenAI").
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Dimension of embeddings produced by this provider.
    /// </summary>
    int Dimensions { get; }

    /// <summary>
    /// Generate an embedding for a single text.
    /// </summary>
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate embeddings for multiple texts in a single API call.
    /// More efficient for batch processing.
    /// </summary>
    Task<List<float[]>> GenerateBatchEmbeddingsAsync(
        IEnumerable<string> texts,
        CancellationToken cancellationToken = default);
}
