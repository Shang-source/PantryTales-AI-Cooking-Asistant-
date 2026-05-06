namespace backend.Interfaces;

/// <summary>
/// Service for generating and updating embeddings for recipes, ingredients, and users.
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Process a batch of recipes that need embeddings.
    /// </summary>
    Task<int> ProcessRecipeBatchAsync(int batchSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Process a batch of ingredients that need embeddings.
    /// </summary>
    Task<int> ProcessIngredientBatchAsync(int batchSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Process a batch of users that need embeddings.
    /// </summary>
    Task<int> ProcessUserBatchAsync(int batchSize, CancellationToken cancellationToken = default);
}
