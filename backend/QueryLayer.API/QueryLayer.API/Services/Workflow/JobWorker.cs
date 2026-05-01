using System.Text.Json;
using QueryLayer.Api.Models.Runtime;
using QueryLayer.Api.Services.Runtime;

namespace QueryLayer.Api.Services.Workflow;

public class JobWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<JobWorker> _logger;
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);

    public JobWorker(IServiceScopeFactory scopeFactory, ILogger<JobWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("JobWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "JobWorker batch error");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    private async Task ProcessBatchAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var jobQueue = scope.ServiceProvider.GetRequiredService<JobQueueService>();
        var actionExecutor = scope.ServiceProvider.GetRequiredService<ActionExecutor>();
        var specService = scope.ServiceProvider.GetRequiredService<SpecService>();

        var jobs = await jobQueue.DequeueAsync();
        if (jobs.Count == 0) return;

        _logger.LogInformation("Processing {Count} jobs", jobs.Count);

        foreach (var job in jobs)
        {
            await jobQueue.MarkProcessingAsync(job);

            try
            {
                var action = JsonSerializer.Deserialize<ActionSpec>(job.ActionJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                var ctxData = JsonSerializer.Deserialize<JobContextData>(job.ContextJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (action == null || ctxData == null)
                {
                    await jobQueue.MarkFailedAsync(job, "Failed to deserialize job data");
                    continue;
                }

                var spec = await specService.GetSpecByProjectIdAsync(job.ProjectId);
                if (spec == null)
                {
                    await jobQueue.MarkFailedAsync(job, "Project spec not found");
                    continue;
                }

                var ctx = new ExecutionContext
                {
                    Record = ctxData.Record ?? new(),
                    Auth = ctxData.Auth ?? new(),
                    ProjectId = job.ProjectId,
                    Spec = spec
                };

                await actionExecutor.ExecuteAsync(action, ctx);
                await jobQueue.MarkSuccessAsync(job);

                _logger.LogInformation("Job {Id} completed: {Type}", job.Id, action.Type);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Job {Id} failed", job.Id);
                await jobQueue.MarkFailedAsync(job, ex.Message);
            }
        }
    }

    private class JobContextData
    {
        public Dictionary<string, object?>? Record { get; set; }
        public Dictionary<string, object?>? Auth { get; set; }
        public Guid ProjectId { get; set; }
    }
}
