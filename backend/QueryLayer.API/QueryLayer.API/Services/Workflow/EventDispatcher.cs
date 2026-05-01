namespace QueryLayer.Api.Services.Workflow;

public class CrudEvent
{
    public string EventName { get; set; } = string.Empty; // e.g. "tasks.created"
    public Guid ProjectId { get; set; }
    public string Entity { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public Dictionary<string, object?> Record { get; set; } = new();
    public Dictionary<string, object?>? PreviousRecord { get; set; }
    public Dictionary<string, object?> AuthContext { get; set; } = new();
}

public class EventDispatcher
{
    private readonly TriggerService _triggerService;
    private readonly ILogger<EventDispatcher> _logger;

    public EventDispatcher(TriggerService triggerService, ILogger<EventDispatcher> logger)
    {
        _triggerService = triggerService;
        _logger = logger;
    }

    public async Task DispatchAsync(CrudEvent crudEvent)
    {
        _logger.LogInformation("Event dispatched: {Event} for project {ProjectId}",
            crudEvent.EventName, crudEvent.ProjectId);

        try
        {
            await _triggerService.ProcessEventAsync(crudEvent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing event {Event}", crudEvent.EventName);
            // Non-blocking: don't throw — CRUD already completed
        }
    }
}
