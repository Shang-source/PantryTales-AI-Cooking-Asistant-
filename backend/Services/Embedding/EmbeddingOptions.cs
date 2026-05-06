namespace backend.Services.Embedding;

/// <summary>
/// Configuration options for embedding generation service (OpenAI).
/// </summary>
public class EmbeddingOptions
{
    public const string SectionName = "Embedding";

    /// <summary>
    /// OpenAI API key for embedding generation.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Model to use for embeddings. Default: text-embedding-3-small.
    /// </summary>
    public string Model { get; set; } = "text-embedding-3-small";

    /// <summary>
    /// Dimensions of the embedding vector. Default: 768.
    /// </summary>
    public int Dimensions { get; set; } = 768;

    /// <summary>
    /// Interval in seconds between embedding generation runs. Default: 300 (5 minutes).
    /// </summary>
    public int IntervalSeconds { get; set; } = 300;

    /// <summary>
    /// Number of entities to process per batch. Default: 50.
    /// </summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>
    /// Whether the background service is enabled. Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
