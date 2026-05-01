using System.Text.Json;
using QueryLayer.Api.Data;
using QueryLayer.Api.Services.Runtime;

namespace QueryLayer.Api.Services.Workflow;

public class TriggerService
{
    private readonly ConditionEvaluator _conditionEvaluator;
    private readonly JobQueueService _jobQueue;
    private readonly AppDbContext _db;
    private readonly SpecService _specService;
    private readonly ILogger<TriggerService> _logger;

    public TriggerService(
        ConditionEvaluator conditionEvaluator,
        JobQueueService jobQueue,
        AppDbContext db,
        SpecService specService,
        ILogger<TriggerService> logger)
    {
        _conditionEvaluator = conditionEvaluator;
        _jobQueue = jobQueue;
        _db = db;
        _specService = specService;
        _logger = logger;
    }

    public async Task ProcessEventAsync(CrudEvent crudEvent)
    {
        var spec = await _specService.GetSpecByProjectIdAsync(crudEvent.ProjectId);
        if (spec == null || spec.Triggers.Count == 0)
            return;

        var matchedTriggers = spec.Triggers
            .Where(t => string.Equals(t.Event, crudEvent.EventName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var trigger in matchedTriggers)
        {
            var conditionResult = _conditionEvaluator.Evaluate(trigger.Condition, crudEvent.Record, crudEvent.PreviousRecord);

            _logger.LogInformation(
                "Trigger {Name}: event={Event}, condition={Condition}, result={Result}",
                trigger.Name, trigger.Event, trigger.Condition ?? "(none)", conditionResult);

            var execution = new TriggerExecution
            {
                ProjectId = crudEvent.ProjectId,
                TriggerName = trigger.Name,
                Event = crudEvent.EventName,
                ConditionResult = conditionResult,
                Status = conditionResult ? "pending" : "skipped",
                RecordJson = JsonSerializer.Serialize(crudEvent.Record)
            };

            _db.TriggerExecutions.Add(execution);
            await _db.SaveChangesAsync();

            if (!conditionResult)
                continue;

            // Build execution context
            var ctx = new ExecutionContext
            {
                Record = crudEvent.Record,
                Auth = crudEvent.AuthContext,
                ProjectId = crudEvent.ProjectId,
                Spec = spec
            };

            // Enqueue each action
            DateTime? runAfter = null;
            foreach (var action in trigger.Actions)
            {
                if (action.Type.Equals("delay", StringComparison.OrdinalIgnoreCase))
                {
                    runAfter = DateTime.UtcNow.Add(ParseDuration(action.Duration));
                    continue;
                }

                await _jobQueue.EnqueueAsync(execution.Id, crudEvent.ProjectId, action, ctx, runAfter);
                runAfter = null; // Reset after applying to next action
            }

            execution.Status = "queued";
            await _db.SaveChangesAsync();
        }
    }

    private static TimeSpan ParseDuration(string? duration)
    {
        if (string.IsNullOrWhiteSpace(duration)) return TimeSpan.Zero;

        duration = duration.Trim().ToLowerInvariant();
        if (duration.EndsWith("ms") && int.TryParse(duration[..^2], out var ms))
            return TimeSpan.FromMilliseconds(ms);
        if (duration.EndsWith("s") && int.TryParse(duration[..^1], out var s))
            return TimeSpan.FromSeconds(s);
        if (duration.EndsWith("m") && int.TryParse(duration[..^1], out var m))
            return TimeSpan.FromMinutes(m);
        if (duration.EndsWith("h") && int.TryParse(duration[..^1], out var h))
            return TimeSpan.FromHours(h);

        return TimeSpan.Zero;
    }
}
