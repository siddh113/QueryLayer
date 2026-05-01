using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QueryLayer.Api.Data;
using QueryLayer.Api.Models.Runtime;

namespace QueryLayer.Api.Services.Workflow;

public class JobQueueService
{
    private readonly AppDbContext _db;
    private readonly ILogger<JobQueueService> _logger;
    private const int MaxAttempts = 3;

    public JobQueueService(AppDbContext db, ILogger<JobQueueService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task EnqueueAsync(
        Guid triggerExecutionId,
        Guid projectId,
        ActionSpec action,
        ExecutionContext ctx,
        DateTime? runAfter = null)
    {
        var job = new Job
        {
            ProjectId = projectId,
            TriggerExecutionId = triggerExecutionId,
            ActionType = action.Type,
            ActionJson = JsonSerializer.Serialize(action),
            ContextJson = JsonSerializer.Serialize(new
            {
                record = ctx.Record,
                auth = ctx.Auth,
                project_id = ctx.ProjectId
            }),
            Status = "queued",
            RunAfter = runAfter
        };

        _db.Jobs.Add(job);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Job {Id} enqueued: type={Type}, runAfter={RunAfter}",
            job.Id, action.Type, runAfter);
    }

    public async Task<List<Job>> DequeueAsync(int batchSize = 10)
    {
        var now = DateTime.UtcNow;
        return await _db.Jobs
            .Where(j => j.Status == "queued" &&
                        (j.RunAfter == null || j.RunAfter <= now) &&
                        j.Attempts < MaxAttempts)
            .OrderBy(j => j.CreatedAt)
            .Take(batchSize)
            .ToListAsync();
    }

    public async Task MarkProcessingAsync(Job job)
    {
        job.Status = "processing";
        job.Attempts++;
        await _db.SaveChangesAsync();
    }

    public async Task MarkSuccessAsync(Job job)
    {
        job.Status = "success";
        job.CompletedAt = DateTime.UtcNow;
        job.ErrorMessage = null;

        _db.JobAttempts.Add(new JobAttempt
        {
            JobId = job.Id,
            AttemptNumber = job.Attempts,
            Status = "success"
        });

        await _db.SaveChangesAsync();
    }

    public async Task MarkFailedAsync(Job job, string error)
    {
        _db.JobAttempts.Add(new JobAttempt
        {
            JobId = job.Id,
            AttemptNumber = job.Attempts,
            Status = "failed",
            ErrorMessage = error
        });

        if (job.Attempts >= MaxAttempts)
        {
            job.Status = "failed";
            job.CompletedAt = DateTime.UtcNow;
            job.ErrorMessage = error;
            _logger.LogError("Job {Id} permanently failed after {Attempts} attempts: {Error}",
                job.Id, job.Attempts, error);
        }
        else
        {
            // Retry with exponential backoff
            job.Status = "queued";
            job.RunAfter = DateTime.UtcNow.AddSeconds(Math.Pow(2, job.Attempts) * 5);
            _logger.LogWarning("Job {Id} attempt {Attempts} failed, retry at {RetryAt}: {Error}",
                job.Id, job.Attempts, job.RunAfter, error);
        }

        await _db.SaveChangesAsync();
    }
}
