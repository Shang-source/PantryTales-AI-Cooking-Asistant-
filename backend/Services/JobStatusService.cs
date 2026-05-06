using System.Collections.Concurrent;

namespace backend.Services;

public class JobStatusService
{
    private readonly ConcurrentDictionary<string, JobStatus> _statuses = new();

    public void UpdateStatus(string jobName, string status, string? message = null)
    {
        _statuses.AddOrUpdate(jobName,
            new JobStatus { JobName = jobName, Status = status, LastUpdate = DateTime.UtcNow, Message = message },
            (key, existing) =>
            {
                existing.Status = status;
                existing.LastUpdate = DateTime.UtcNow;
                existing.Message = message;
                return existing;
            });
    }

    public void RecordExecution(string jobName, bool success, string? error = null)
    {
        _statuses.AddOrUpdate(jobName,
           new JobStatus { JobName = jobName, LastRun = DateTime.UtcNow, LastRunSuccess = success, LastError = error, Status = "Idle" },
           (key, existing) =>
           {
               existing.LastRun = DateTime.UtcNow;
               existing.LastRunSuccess = success;
               existing.LastError = error;
               existing.Status = "Idle";
               return existing;
           });
    }

    public List<JobStatus> GetAllStatuses() => _statuses.Values.OrderBy(x => x.JobName).ToList();
}

public class JobStatus
{
    public string JobName { get; set; } = string.Empty;
    public string Status { get; set; } = "Unknown"; // Idle, Running
    public DateTime LastUpdate { get; set; }
    public DateTime? LastRun { get; set; }
    public bool? LastRunSuccess { get; set; }
    public string? LastError { get; set; }
    public string? Message { get; set; }
}
