using Microsoft.EntityFrameworkCore;
using QueryLayer.Api.Data;
using QueryLayer.Api.Models.Runtime;

namespace QueryLayer.Api.Services.Workflow;

public class WorkflowEngine
{
    private readonly AppDbContext _db;
    private readonly ILogger<WorkflowEngine> _logger;

    public WorkflowEngine(AppDbContext db, ILogger<WorkflowEngine> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<WorkflowTransitionResult> TransitionAsync(
        BackendSpec spec,
        Guid projectId,
        string entity,
        string recordId,
        string transitionName,
        string? currentState,
        string? userRole,
        Guid? userId)
    {
        var workflow = spec.Workflows.FirstOrDefault(w =>
            w.Entity.Equals(entity, StringComparison.OrdinalIgnoreCase));

        if (workflow == null)
            return WorkflowTransitionResult.Error("No workflow defined for entity");

        var transition = workflow.Transitions.FirstOrDefault(t =>
            t.Name.Equals(transitionName, StringComparison.OrdinalIgnoreCase));

        if (transition == null)
            return WorkflowTransitionResult.Error($"Transition '{transitionName}' not found");

        // Validate current state
        var expectedFrom = transition.From;
        if (!string.Equals(currentState, expectedFrom, StringComparison.OrdinalIgnoreCase))
            return WorkflowTransitionResult.Error(
                $"Invalid state transition: record is in '{currentState}', expected '{expectedFrom}'");

        // Validate role
        if (transition.Roles.Count > 0 &&
            (userRole == null || !transition.Roles.Any(r =>
                r.Equals(userRole, StringComparison.OrdinalIgnoreCase))))
            return WorkflowTransitionResult.Error(
                $"Role '{userRole}' not allowed to perform transition '{transitionName}'");

        // Validate target state exists
        if (!workflow.States.Contains(transition.To, StringComparer.OrdinalIgnoreCase))
            return WorkflowTransitionResult.Error($"Target state '{transition.To}' not defined");

        // Log execution
        _db.WorkflowExecutions.Add(new WorkflowExecution
        {
            ProjectId = projectId,
            Entity = entity,
            RecordId = recordId,
            FromState = currentState ?? "",
            ToState = transition.To,
            TransitionName = transitionName,
            UserId = userId,
            UserRole = userRole
        });
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Workflow transition: entity={Entity}, record={RecordId}, {From}→{To} by role={Role}",
            entity, recordId, currentState, transition.To, userRole);

        return WorkflowTransitionResult.Success(transition.To);
    }
}

public class WorkflowTransitionResult
{
    public bool IsSuccess { get; private set; }
    public string? NewState { get; private set; }
    public string? ErrorMessage { get; private set; }

    public static WorkflowTransitionResult Success(string newState) =>
        new() { IsSuccess = true, NewState = newState };

    public static WorkflowTransitionResult Error(string message) =>
        new() { IsSuccess = false, ErrorMessage = message };
}
