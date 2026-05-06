using backend.Interfaces;
using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Pages.Admin.Jobs;

public class IndexModel(
    JobStatusService jobStatus,
    INameNormalizationService normalizationService,
    ILogger<IndexModel> logger) : AdminPageModel
{
    public List<JobStatus> Statuses { get; set; } = [];

    public void OnGet()
    {
        Statuses = jobStatus.GetAllStatuses();
    }

    public async Task<IActionResult> OnPostTriggerNormalizationAsync()
    {
        logger.LogInformation("Manually triggered name normalization.");
        // We can't easily interrupt the background service, but we can run a batch immediately
        // using the scoped service, which might pick up items before the background service does.
        try
        {
            await normalizationService.ProcessInventoryItemBatchAsync(100);
            jobStatus.UpdateStatus("NameNormalization (Manual)", "Idle", "Manual run completed");
            TempData["Message"] = "Manual normalization batch triggered successfully.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Manual normalization failed");
            TempData["Error"] = "Manual run failed: " + ex.Message;
        }

        return RedirectToPage();
    }
}
