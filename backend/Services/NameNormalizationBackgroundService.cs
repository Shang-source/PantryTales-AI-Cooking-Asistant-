using backend.Interfaces;
using Microsoft.Extensions.Options;

namespace backend.Services;

public class NameNormalizationOptions
{
    public const string SectionName = "NameNormalization";

    /// <summary>
    /// Interval in seconds between normalization runs. Default: 60 (1 minute).
    /// </summary>
    public int IntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Number of items to process per batch. Default: 100.
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Whether the background service is enabled. Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;
}

public class NameNormalizationBackgroundService(
    IServiceProvider serviceProvider,
    IOptions<NameNormalizationOptions> options,
    JobStatusService jobStatus,
    ILogger<NameNormalizationBackgroundService> logger) : BackgroundService
{
    private readonly NameNormalizationOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Name normalization background service is disabled.");
            jobStatus.UpdateStatus("NameNormalization", "Disabled");
            return;
        }

        logger.LogInformation(
            "Name normalization background service started. Interval: {Interval}s, BatchSize: {BatchSize}.",
            _options.IntervalSeconds, _options.BatchSize);
        jobStatus.UpdateStatus("NameNormalization", "Starting");

        // Initial delay to let the app start up
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Graceful shutdown
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in name normalization background service.");
                jobStatus.RecordExecution("NameNormalization", false, ex.Message);
                jobStatus.UpdateStatus("NameNormalization", "Error", ex.Message);
            }

            try
            {
                jobStatus.UpdateStatus("NameNormalization", "Idle", "Waiting for next interval");
                await Task.Delay(TimeSpan.FromSeconds(_options.IntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("Name normalization background service stopped.");
        jobStatus.UpdateStatus("NameNormalization", "Stopped");
    }

    private async Task ProcessBatchAsync(CancellationToken stoppingToken)
    {
        jobStatus.UpdateStatus("NameNormalization", "Running");
        using var scope = serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<INameNormalizationService>();

        var processed = await service.ProcessInventoryItemBatchAsync(_options.BatchSize, stoppingToken);

        if (processed > 0)
        {
            logger.LogDebug("Processed {Count} inventory items for normalization.", processed);
            jobStatus.RecordExecution("NameNormalization", true, $"Processed {processed} items");
        }
        else
        {
            jobStatus.RecordExecution("NameNormalization", true, "No items to process");
        }
    }
}
