using backend.Data;
using backend.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace backend.Services.ImageGeneration;

public class RecipeImageGenerationOptions
{
    public const string SectionName = "RecipeImageGeneration";

    /// <summary>
    /// Interval in seconds between image generation runs. Default: 900 (15 minutes).
    /// </summary>
    public int IntervalSeconds { get; set; } = 900;

    /// <summary>
    /// Number of recipes to process per batch. Default: 20.
    /// </summary>
    public int BatchSize { get; set; } = 20;

    /// <summary>
    /// Initial delay before the first run. Default: 30 seconds.
    /// </summary>
    public int StartupDelaySeconds { get; set; } = 30;

    /// <summary>
    /// Whether the background service is enabled. Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Background service that periodically generates missing recipe cover images.
/// </summary>
public class RecipeImageBackgroundService(
    IServiceProvider serviceProvider,
    IOptions<RecipeImageGenerationOptions> options,
    JobStatusService jobStatus,
    ILogger<RecipeImageBackgroundService> logger)
    : BackgroundService
{
    private readonly RecipeImageGenerationOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Recipe image background service is disabled.");
            jobStatus.UpdateStatus("RecipeImages", "Disabled");
            return;
        }

        logger.LogInformation(
            "Recipe image background service started. Interval: {Interval}s, BatchSize: {BatchSize}.",
            _options.IntervalSeconds, _options.BatchSize);
        jobStatus.UpdateStatus("RecipeImages", "Starting");

        // Initial delay to let the app start up
        await Task.Delay(TimeSpan.FromSeconds(_options.StartupDelaySeconds), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in recipe image background service.");
                jobStatus.RecordExecution("RecipeImages", false, ex.Message);
                jobStatus.UpdateStatus("RecipeImages", "Error", ex.Message);
            }

            try
            {
                jobStatus.UpdateStatus("RecipeImages", "Idle", "Waiting for next interval");
                await Task.Delay(TimeSpan.FromSeconds(_options.IntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("Recipe image background service stopped.");
        jobStatus.UpdateStatus("RecipeImages", "Stopped");
    }

    private async Task ProcessBatchAsync(CancellationToken stoppingToken)
    {
        jobStatus.UpdateStatus("RecipeImages", "Running");
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var recipeImageService = scope.ServiceProvider.GetRequiredService<IRecipeImageService>();

        var recipes = await dbContext.Recipes
            .Where(r => r.ImageUrls == null || r.ImageUrls.Count == 0)
            .OrderBy(r => r.CreatedAt)
            .Include(r => r.Ingredients)
                .ThenInclude(ri => ri.Ingredient)
            .Include(r => r.Tags)
                .ThenInclude(rt => rt.Tag)
            .Take(_options.BatchSize)
            .ToListAsync(stoppingToken);

        if (recipes.Count == 0)
        {
            jobStatus.RecordExecution("RecipeImages", true, "No recipes to process");
            return;
        }

        var successCount = 0;
        var failureCount = 0;

        foreach (var recipe in recipes)
        {
            var url = await recipeImageService.EnsureCoverImageUrlAsync(recipe, stoppingToken);
            if (!string.IsNullOrWhiteSpace(url))
            {
                successCount++;
            }
            else
            {
                failureCount++;
            }
        }

        logger.LogInformation(
            "Recipe image generation processed {Total} recipes: {Success} succeeded, {Failed} failed.",
            recipes.Count, successCount, failureCount);

        jobStatus.RecordExecution(
            "RecipeImages",
            failureCount == 0,
            $"Generated {successCount}/{recipes.Count} images");
    }
}
