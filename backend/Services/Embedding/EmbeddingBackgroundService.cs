using backend.Interfaces;
using Microsoft.Extensions.Options;

namespace backend.Services.Embedding;

/// <summary>
/// Background service that periodically generates embeddings for recipes, ingredients, and users.
/// </summary>
public class EmbeddingBackgroundService(
    IServiceProvider serviceProvider,
    IOptions<EmbeddingOptions> options,
    JobStatusService jobStatus,
    ILogger<EmbeddingBackgroundService> logger)
    : BackgroundService
{
    private readonly EmbeddingOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Embedding background service is disabled.");
            jobStatus.UpdateStatus("Embedding", "Disabled");
            return;
        }

        logger.LogInformation(
            "Embedding background service started. Provider: OpenAI, Interval: {Interval}s, BatchSize: {BatchSize}.",
            _options.IntervalSeconds, _options.BatchSize);
        jobStatus.UpdateStatus("Embedding", "Starting");

        // Initial delay to let the app start up
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessAllBatchesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in embedding background service.");
                jobStatus.RecordExecution("Embedding", false, ex.Message);
                jobStatus.UpdateStatus("Embedding", "Error", ex.Message);
            }

            try
            {
                jobStatus.UpdateStatus("Embedding", "Idle", "Waiting for next interval");
                await Task.Delay(TimeSpan.FromSeconds(_options.IntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("Embedding background service stopped.");
        jobStatus.UpdateStatus("Embedding", "Stopped");
    }

    private async Task ProcessAllBatchesAsync(CancellationToken stoppingToken)
    {
        jobStatus.UpdateStatus("Embedding", "Running");
        using var scope = serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();

        // Process entity types sequentially since they share the same DbContext
        // DbContext is not thread-safe and cannot handle concurrent operations
        var recipeCount = await service.ProcessRecipeBatchAsync(_options.BatchSize, stoppingToken);
        var ingredientCount = await service.ProcessIngredientBatchAsync(_options.BatchSize, stoppingToken);
        var userCount = await service.ProcessUserBatchAsync(_options.BatchSize, stoppingToken);

        var total = recipeCount + ingredientCount + userCount;

        if (total > 0)
        {
            logger.LogDebug("Processed embeddings: {R} recipes, {I} ingredients, {U} users.", recipeCount, ingredientCount, userCount);
            jobStatus.RecordExecution("Embedding", true, $"Processed {total} Items (R:{recipeCount}, I:{ingredientCount}, U:{userCount})");
        }
        else
        {
            jobStatus.RecordExecution("Embedding", true, "No items to process");
        }
    }
}