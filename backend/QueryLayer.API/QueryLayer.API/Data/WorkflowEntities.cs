using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QueryLayer.Api.Data;

public class TriggerExecution
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string TriggerName { get; set; } = string.Empty;
    public string Event { get; set; } = string.Empty;
    public bool ConditionResult { get; set; }
    public string Status { get; set; } = "pending"; // pending, success, failed
    public string? ErrorMessage { get; set; }
    public string? RecordJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

public class Job
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Guid TriggerExecutionId { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string ActionJson { get; set; } = string.Empty;
    public string ContextJson { get; set; } = string.Empty;
    public string Status { get; set; } = "queued"; // queued, processing, success, failed
    public int Attempts { get; set; } = 0;
    public DateTime? RunAfter { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
}

public class JobAttempt
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid JobId { get; set; }
    public int AttemptNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;
}

public class WorkflowExecution
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string Entity { get; set; } = string.Empty;
    public string RecordId { get; set; } = string.Empty;
    public string FromState { get; set; } = string.Empty;
    public string ToState { get; set; } = string.Empty;
    public string TransitionName { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public string? UserRole { get; set; }
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
}
