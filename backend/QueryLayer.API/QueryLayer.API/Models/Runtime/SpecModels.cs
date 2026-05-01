namespace QueryLayer.Api.Models.Runtime;

public class BackendSpec
{
    public List<EntitySpec> Entities { get; set; } = new();
    public List<EndpointSpec> Endpoints { get; set; } = new();
    public List<PermissionSpec> Permissions { get; set; } = new();
    public List<TriggerSpec> Triggers { get; set; } = new();
    public List<WorkflowSpec> Workflows { get; set; } = new();
}

public class EntitySpec
{
    public string Name { get; set; } = string.Empty;
    public string Table { get; set; } = string.Empty;
    public List<FieldSpec> Fields { get; set; } = new();
}

public class FieldSpec
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "string";
    public bool Primary { get; set; }
    public bool Required { get; set; }
    public bool Unique { get; set; }
    public RelationSpec? Relation { get; set; }
}

public class RelationSpec
{
    public string Table { get; set; } = string.Empty;
    public string Column { get; set; } = "id";
}

public class EndpointSpec
{
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string Entity { get; set; } = string.Empty;
    public string? Auth { get; set; }
    public string? Transition { get; set; } // workflow_transition only
}

public class PermissionSpec
{
    public string Entity { get; set; } = string.Empty;
    public List<string> Operations { get; set; } = new();
    public string? Filter { get; set; }
}

// Sprint 8: Workflow + Trigger Engine

public class TriggerSpec
{
    public string Name { get; set; } = string.Empty;
    public string Event { get; set; } = string.Empty;
    public string? Condition { get; set; }
    public List<ActionSpec> Actions { get; set; } = new();
}

public class ActionSpec
{
    public string Type { get; set; } = string.Empty;
    // webhook
    public string? Url { get; set; }
    // create
    public string? Entity { get; set; }
    public Dictionary<string, object?>? Data { get; set; }
    // update
    public Dictionary<string, object?>? Fields { get; set; }
    public string? Where { get; set; }
    public string? Target { get; set; }
    // delay
    public string? Duration { get; set; }
}

public class WorkflowSpec
{
    public string Entity { get; set; } = string.Empty;
    public List<string> States { get; set; } = new();
    public string InitialState { get; set; } = string.Empty;
    public List<TransitionSpec> Transitions { get; set; } = new();
}

public class TransitionSpec
{
    public string Name { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
    public string? Condition { get; set; }
}
