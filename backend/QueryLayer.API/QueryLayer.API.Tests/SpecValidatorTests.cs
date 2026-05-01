using QueryLayer.Api.Models.Runtime;
using QueryLayer.Api.Services.AI;
using Xunit;

namespace QueryLayer.Api.Tests;

/// <summary>
/// Tests for SpecValidator using real ConditionSyntaxValidator (no external deps).
/// </summary>
public class SpecValidatorTests
{
    private readonly SpecValidator _validator = new(new ConditionSyntaxValidator());

    // =========================================================================
    // Helpers
    // =========================================================================

    private static EntitySpec MakeEntity(string name, string table, params FieldSpec[] fields) =>
        new() { Name = name, Table = table, Fields = fields.ToList() };

    private static FieldSpec PrimaryField(string name = "id") =>
        new() { Name = name, Type = "uuid", Primary = true, Required = true };

    private static FieldSpec StringField(string name, bool required = false) =>
        new() { Name = name, Type = "string", Primary = false, Required = required };

    private static EndpointSpec MakeEndpoint(string method, string path, string op, string entity, string auth) =>
        new() { Method = method, Path = path, Operation = op, Entity = entity, Auth = auth };

    private static BackendSpec MinimalValidSpec() =>
        new()
        {
            Entities = new List<EntitySpec>
            {
                MakeEntity("Task", "tasks", PrimaryField(), StringField("title", true))
            },
            Endpoints = new List<EndpointSpec>
            {
                MakeEndpoint("GET", "/tasks", "list", "Task", "authenticated")
            }
        };

    // =========================================================================
    // Entity validation
    // =========================================================================

    [Fact]
    public void ValidSpec_Passes()
    {
        var result = _validator.Validate(MinimalValidSpec());
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Entity_MissingName_ReturnsError()
    {
        var spec = new BackendSpec
        {
            Entities = new List<EntitySpec>
            {
                new() { Name = "", Table = "tasks", Fields = new List<FieldSpec> { PrimaryField() } }
            }
        };
        var result = _validator.Validate(spec);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Path.Contains("name") && e.Message.Contains("required"));
    }

    [Fact]
    public void Entity_MissingTable_ReturnsError()
    {
        var spec = new BackendSpec
        {
            Entities = new List<EntitySpec>
            {
                new() { Name = "Task", Table = "", Fields = new List<FieldSpec> { PrimaryField() } }
            }
        };
        var result = _validator.Validate(spec);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Path.Contains("table") && e.Message.Contains("table name"));
    }

    [Fact]
    public void Entity_ReservedTableName_ReturnsError()
    {
        var spec = new BackendSpec
        {
            Entities = new List<EntitySpec>
            {
                new() { Name = "User", Table = "users", Fields = new List<FieldSpec> { PrimaryField() } }
            }
        };
        var result = _validator.Validate(spec);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("reserved"));
    }

    [Fact]
    public void Entity_DuplicateTableNames_ReturnsError()
    {
        var spec = new BackendSpec
        {
            Entities = new List<EntitySpec>
            {
                MakeEntity("Task", "items", PrimaryField()),
                MakeEntity("Product", "items", PrimaryField())
            }
        };
        var result = _validator.Validate(spec);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("multiple entities"));
    }

    [Fact]
    public void Entity_NoFields_ReturnsError()
    {
        var spec = new BackendSpec
        {
            Entities = new List<EntitySpec>
            {
                new() { Name = "Task", Table = "tasks", Fields = new List<FieldSpec>() }
            }
        };
        var result = _validator.Validate(spec);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("at least one field"));
    }

    [Fact]
    public void Entity_NoPrimaryKey_ReturnsError()
    {
        var spec = new BackendSpec
        {
            Entities = new List<EntitySpec>
            {
                MakeEntity("Task", "tasks", StringField("title"))
            }
        };
        var result = _validator.Validate(spec);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("primary key"));
    }

    [Fact]
    public void Entity_MultiplePrimaryKeys_ReturnsError()
    {
        var spec = new BackendSpec
        {
            Entities = new List<EntitySpec>
            {
                MakeEntity("Task", "tasks",
                    new FieldSpec { Name = "id", Type = "uuid", Primary = true },
                    new FieldSpec { Name = "id2", Type = "uuid", Primary = true })
            }
        };
        var result = _validator.Validate(spec);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("primary key") && e.Message.Contains("2"));
    }

    [Fact]
    public void Entity_DuplicateFieldNames_ReturnsError()
    {
        var spec = new BackendSpec
        {
            Entities = new List<EntitySpec>
            {
                MakeEntity("Task", "tasks",
                    PrimaryField(),
                    StringField("title"),
                    StringField("title"))    // duplicate
            }
        };
        var result = _validator.Validate(spec);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("duplicate field"));
    }

    [Fact]
    public void Entity_InvalidFieldType_ReturnsError()
    {
        var spec = new BackendSpec
        {
            Entities = new List<EntitySpec>
            {
                MakeEntity("Task", "tasks",
                    PrimaryField(),
                    new FieldSpec { Name = "data", Type = "blob" })
            }
        };
        var result = _validator.Validate(spec);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("invalid type") && e.Message.Contains("blob"));
    }

    [Fact]
    public void Entity_DecimalFieldType_IsValid()
    {
        var spec = new BackendSpec
        {
            Entities = new List<EntitySpec>
            {
                MakeEntity("Product", "products",
                    PrimaryField(),
                    new FieldSpec { Name = "price", Type = "decimal" })
            }
        };
        var result = _validator.Validate(spec);
        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.Message)));
    }

    [Theory]
    [InlineData("string")]
    [InlineData("integer")]
    [InlineData("boolean")]
    [InlineData("uuid")]
    [InlineData("timestamp")]
    [InlineData("text")]
    [InlineData("decimal")]
    public void Entity_AllValidFieldTypes_Pass(string fieldType)
    {
        var spec = new BackendSpec
        {
            Entities = new List<EntitySpec>
            {
                MakeEntity("Item", "items",
                    PrimaryField(),
                    new FieldSpec { Name = "value", Type = fieldType })
            }
        };
        var result = _validator.Validate(spec);
        Assert.True(result.IsValid, $"Type '{fieldType}' should be valid. Errors: {string.Join("; ", result.Errors.Select(e => e.Message))}");
    }

    [Fact]
    public void Entity_MissingSpecEntities_ReturnsError()
    {
        var spec = new BackendSpec { Entities = new List<EntitySpec>() };
        var result = _validator.Validate(spec);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("at least one entity"));
    }

    // =========================================================================
    // Endpoint validation
    // =========================================================================

    [Fact]
    public void Endpoint_InvalidMethod_ReturnsError()
    {
        var spec = MinimalValidSpec();
        spec.Endpoints.Add(MakeEndpoint("FETCH", "/tasks/1", "read", "Task", "authenticated"));
        var result = _validator.Validate(spec);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("FETCH"));
    }

    [Fact]
    public void Endpoint_MissingPath_ReturnsError()
    {
        var spec = MinimalValidSpec();
        spec.Endpoints.Add(new EndpointSpec { Method = "GET", Path = "", Operation = "list", Entity = "Task", Auth = "authenticated" });
        var result = _validator.Validate(spec);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("path is required"));
    }

    [Fact]
    public void Endpoint_InvalidOperation_ReturnsError()
    {
        var spec = MinimalValidSpec();
        spec.Endpoints.Add(MakeEndpoint("GET", "/tasks", "query", "Task", "authenticated"));
        var result = _validator.Validate(spec);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("query"));
    }

    [Fact]
    public void Endpoint_UnknownEntity_ReturnsError()
    {
        var spec = MinimalValidSpec();
        spec.Endpoints.Add(MakeEndpoint("GET", "/orders", "list", "Order", "authenticated"));
        var result = _validator.Validate(spec);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("unknown entity") && e.Message.Contains("Order"));
    }

    [Fact]
    public void Endpoint_MissingAuth_ReturnsError()
    {
        var spec = MinimalValidSpec();
        spec.Endpoints.Add(new EndpointSpec { Method = "GET", Path = "/tasks/1", Operation = "read", Entity = "Task", Auth = null });
        var result = _validator.Validate(spec);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("missing auth mode"));
    }

    [Fact]
    public void Endpoint_InvalidAuth_ReturnsError()
    {
        var spec = MinimalValidSpec();
        spec.Endpoints.Add(MakeEndpoint("GET", "/tasks/1", "read", "Task", "superadmin"));
        var result = _validator.Validate(spec);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("superadmin"));
    }

    [Fact]
    public void Endpoint_ServiceAuth_IsValid()
    {
        var spec = MinimalValidSpec();
        spec.Endpoints.Add(MakeEndpoint("GET", "/tasks", "list", "Task", "service"));
        var result = _validator.Validate(spec);
        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.Message)));
    }

    [Fact]
    public void Endpoint_WorkflowTransition_MissingTransitionName_ReturnsError()
    {
        var spec = MinimalValidSpec();
        spec.Endpoints.Add(new EndpointSpec
        {
            Method = "POST",
            Path = "/tasks/{id}/approve",
            Operation = "workflow_transition",
            Entity = "Task",
            Auth = "authenticated",
            Transition = null
        });
        var result = _validator.Validate(spec);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("missing 'transition' name"));
    }

    [Fact]
    public void Endpoint_WorkflowTransition_UnknownTransitionName_ReturnsError()
    {
        var spec = MinimalValidSpec();

        // Add state field required for workflow
        spec.Entities[0].Fields.Add(new FieldSpec { Name = "state", Type = "string", Required = true });

        spec.Workflows = new List<WorkflowSpec>
        {
            new()
            {
                Entity = "Task",
                States = new List<string> { "open", "closed" },
                InitialState = "open",
                Transitions = new List<TransitionSpec>
                {
                    new() { Name = "close", From = "open", To = "closed", Roles = new List<string> { "authenticated" } }
                }
            }
        };

        // Endpoint references transition "nonexistent" which doesn't exist in workflow
        spec.Endpoints.Add(new EndpointSpec
        {
            Method = "POST",
            Path = "/tasks/{id}/nonexistent",
            Operation = "workflow_transition",
            Entity = "Task",
            Auth = "authenticated",
            Transition = "nonexistent"
        });

        var result = _validator.Validate(spec);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("unknown transition") && e.Message.Contains("nonexistent"));
    }

    // =========================================================================
    // Trigger validation
    // =========================================================================

    [Fact]
    public void Trigger_ValidEvent_Passes()
    {
        var spec = MinimalValidSpec();
        spec.Triggers = new List<TriggerSpec>
        {
            new()
            {
                Name = "on_task_created",
                Event = "tasks.created",
                Actions = new List<ActionSpec>
                {
                    new() { Type = "webhook", Url = "https://example.com/hook" }
                }
            }
        };
        var result = _validator.Validate(spec);
        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.Message)));
    }

    [Fact]
    public void Trigger_EventWithNoDot_ReturnsError()
    {
        var spec = MinimalValidSpec();
        spec.Triggers = new List<TriggerSpec>
        {
            new()
            {
                Name = "t1",
                Event = "tasks",
                Actions = new List<ActionSpec> { new() { Type = "webhook", Url = "https://example.com" } }
            }
        };
        var result = _validator.Validate(spec);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("table.operation"));
    }

    [Fact]
    public void Trigger_InvalidEventOperation_ReturnsError()
    {
        var spec = MinimalValidSpec();
        spec.Triggers = new List<TriggerSpec>
        {
            new()
            {
                Name = "t1",
                Event = "tasks.saved",
                Actions = new List<ActionSpec> { new() { Type = "webhook", Url = "https://example.com" } }
            }
        };
        var result = _validator.Validate(spec);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("saved"));
    }

    [Fact]
    public void Trigger_UnknownTableInEvent_ReturnsError()
    {
        var spec = MinimalValidSpec();
        spec.Triggers = new List<TriggerSpec>
        {
            new()
            {
                Name = "t1",
                Event = "orders.created",
                Actions = new List<ActionSpec> { new() { Type = "webhook", Url = "https://example.com" } }
            }
        };
        var result = _validator.Validate(spec);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("unknown table") && e.Message.Contains("orders"));
    }

    [Fact]
    public void Trigger_EmptyActionsArray_ReturnsError()
    {
        var spec = MinimalValidSpec();
        spec.Triggers = new List<TriggerSpec>
        {
            new() { Name = "t1", Event = "tasks.created", Actions = new List<ActionSpec>() }
        };
        var result = _validator.Validate(spec);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("at least one action"));
    }

    [Fact]
    public void Trigger_WebhookAction_MissingUrl_ReturnsError()
    {
        var spec = MinimalValidSpec();
        spec.Triggers = new List<TriggerSpec>
        {
            new()
            {
                Name = "t1",
                Event = "tasks.created",
                Actions = new List<ActionSpec> { new() { Type = "webhook", Url = null } }
            }
        };
        var result = _validator.Validate(spec);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("missing URL"));
    }

    [Fact]
    public void Trigger_WebhookAction_UrlNotHttp_ReturnsError()
    {
        var spec = MinimalValidSpec();
        spec.Triggers = new List<TriggerSpec>
        {
            new()
            {
                Name = "t1",
                Event = "tasks.created",
                Actions = new List<ActionSpec> { new() { Type = "webhook", Url = "ftp://example.com" } }
            }
        };
        var result = _validator.Validate(spec);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("http://") || e.Message.Contains("https://"));
    }

    [Fact]
    public void Trigger_CreateAction_MissingEntity_ReturnsError()
    {
        var spec = MinimalValidSpec();
        spec.Triggers = new List<TriggerSpec>
        {
            new()
            {
                Name = "t1",
                Event = "tasks.created",
                Actions = new List<ActionSpec>
                {
                    new() { Type = "create", Entity = null, Data = new Dictionary<string, object?> { { "x", "y" } } }
                }
            }
        };
        var result = _validator.Validate(spec);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("missing entity"));
    }

    [Fact]
    public void Trigger_UpdateAction_MissingFields_ReturnsError()
    {
        var spec = MinimalValidSpec();
        spec.Triggers = new List<TriggerSpec>
        {
            new()
            {
                Name = "t1",
                Event = "tasks.created",
                Actions = new List<ActionSpec>
                {
                    new() { Type = "update", Entity = "Task", Fields = null }
                }
            }
        };
        var result = _validator.Validate(spec);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("missing fields"));
    }

    [Fact]
    public void Trigger_DelayAction_InvalidDuration_ReturnsError()
    {
        var spec = MinimalValidSpec();
        spec.Triggers = new List<TriggerSpec>
        {
            new()
            {
                Name = "t1",
                Event = "tasks.created",
                Actions = new List<ActionSpec>
                {
                    new() { Type = "delay", Duration = "2d" }
                }
            }
        };
        var result = _validator.Validate(spec);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("2d") || e.Message.Contains("invalid"));
    }

    [Theory]
    [InlineData("10s")]
    [InlineData("5m")]
    [InlineData("1h")]
    public void Trigger_DelayAction_ValidDuration_Passes(string duration)
    {
        var spec = MinimalValidSpec();
        spec.Triggers = new List<TriggerSpec>
        {
            new()
            {
                Name = "t1",
                Event = "tasks.created",
                Actions = new List<ActionSpec>
                {
                    new() { Type = "delay", Duration = duration }
                }
            }
        };
        var result = _validator.Validate(spec);
        Assert.True(result.IsValid, $"Duration '{duration}' should be valid. Errors: {string.Join("; ", result.Errors.Select(e => e.Message))}");
    }

    // =========================================================================
    // Workflow validation
    // =========================================================================

    private BackendSpec MakeWorkflowSpec(
        string entityName = "PurchaseRequest",
        string tableName = "purchase_requests",
        string initialState = "draft",
        List<string>? states = null,
        List<TransitionSpec>? transitions = null,
        bool addStateField = true)
    {
        var fields = new List<FieldSpec> { PrimaryField(), new() { Name = "amount", Type = "decimal" } };
        if (addStateField)
            fields.Add(new FieldSpec { Name = "state", Type = "string", Required = true });

        states ??= new List<string> { "draft", "submitted", "approved", "rejected" };
        transitions ??= new List<TransitionSpec>
        {
            new() { Name = "submit", From = "draft", To = "submitted", Roles = new List<string> { "employee" } },
            new() { Name = "approve", From = "submitted", To = "approved", Roles = new List<string> { "manager" } },
            new() { Name = "reject", From = "submitted", To = "rejected", Roles = new List<string> { "manager" } }
        };

        var spec = new BackendSpec
        {
            Entities = new List<EntitySpec>
            {
                new() { Name = entityName, Table = tableName, Fields = fields }
            },
            Endpoints = new List<EndpointSpec>(),
            Workflows = new List<WorkflowSpec>
            {
                new()
                {
                    Entity = entityName,
                    States = states,
                    InitialState = initialState,
                    Transitions = transitions
                }
            }
        };

        // Add matching workflow_transition endpoints
        foreach (var t in transitions)
        {
            spec.Endpoints.Add(new EndpointSpec
            {
                Method = "POST",
                Path = $"/{tableName}/{{id}}/{t.Name}",
                Operation = "workflow_transition",
                Entity = entityName,
                Auth = "authenticated",
                Transition = t.Name
            });
        }

        return spec;
    }

    [Fact]
    public void Workflow_ValidSpec_Passes()
    {
        var spec = MakeWorkflowSpec();
        var result = _validator.Validate(spec);
        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.Message)));
    }

    [Fact]
    public void Workflow_EmptyInitialState_ReturnsError()
    {
        var spec = MakeWorkflowSpec(initialState: "");
        var result = _validator.Validate(spec);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Path.Contains("initialState") && e.Message.Contains("required"));
    }

    [Fact]
    public void Workflow_InitialStateNotInStates_ReturnsError()
    {
        var spec = MakeWorkflowSpec(initialState: "pending");
        var result = _validator.Validate(spec);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("not in the states list"));
    }

    [Fact]
    public void Workflow_EmptyStatesArray_ReturnsError()
    {
        var spec = MakeWorkflowSpec(states: new List<string>());
        var result = _validator.Validate(spec);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("at least one state"));
    }

    [Fact]
    public void Workflow_EmptyTransitions_ReturnsError()
    {
        var spec = MakeWorkflowSpec(transitions: new List<TransitionSpec>());
        spec.Endpoints.Clear();
        var result = _validator.Validate(spec);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("at least one transition"));
    }

    [Fact]
    public void Workflow_TransitionFromUnknownState_ReturnsError()
    {
        var transitions = new List<TransitionSpec>
        {
            new() { Name = "submit", From = "nonexistent", To = "submitted", Roles = new List<string> { "employee" } },
            new() { Name = "approve", From = "submitted", To = "approved", Roles = new List<string> { "manager" } },
            new() { Name = "reject", From = "submitted", To = "rejected", Roles = new List<string> { "manager" } }
        };
        var spec = MakeWorkflowSpec(transitions: transitions);
        var result = _validator.Validate(spec);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("unknown 'from' state") && e.Message.Contains("nonexistent"));
    }

    [Fact]
    public void Workflow_TransitionToUnknownState_ReturnsError()
    {
        var transitions = new List<TransitionSpec>
        {
            new() { Name = "submit", From = "draft", To = "limbo", Roles = new List<string> { "employee" } },
            new() { Name = "approve", From = "submitted", To = "approved", Roles = new List<string> { "manager" } },
            new() { Name = "reject", From = "submitted", To = "rejected", Roles = new List<string> { "manager" } }
        };
        var spec = MakeWorkflowSpec(transitions: transitions);
        // Remove the auto-generated submit endpoint and replace with correct one to avoid extra errors
        spec.Endpoints.Clear();
        foreach (var t in transitions)
            spec.Endpoints.Add(new EndpointSpec { Method = "POST", Path = $"/purchase_requests/{{id}}/{t.Name}", Operation = "workflow_transition", Entity = "PurchaseRequest", Auth = "authenticated", Transition = t.Name });

        var result = _validator.Validate(spec);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("unknown 'to' state") && e.Message.Contains("limbo"));
    }

    [Fact]
    public void Workflow_TransitionWithNoRoles_ReturnsError()
    {
        var transitions = new List<TransitionSpec>
        {
            new() { Name = "submit", From = "draft", To = "submitted", Roles = new List<string>() },
            new() { Name = "approve", From = "submitted", To = "approved", Roles = new List<string> { "manager" } },
            new() { Name = "reject", From = "submitted", To = "rejected", Roles = new List<string> { "manager" } }
        };
        var spec = MakeWorkflowSpec(transitions: transitions);
        var result = _validator.Validate(spec);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("at least one role") && e.Message.Contains("submit"));
    }

    [Fact]
    public void Workflow_EntityMissingStateField_ReturnsError()
    {
        var spec = MakeWorkflowSpec(addStateField: false);
        var result = _validator.Validate(spec);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("missing a 'state' field"));
    }

    [Fact]
    public void Workflow_ReferencingUnknownEntity_ReturnsError()
    {
        var spec = MinimalValidSpec();
        spec.Workflows = new List<WorkflowSpec>
        {
            new()
            {
                Entity = "NonExistent",
                States = new List<string> { "open", "closed" },
                InitialState = "open",
                Transitions = new List<TransitionSpec>
                {
                    new() { Name = "close", From = "open", To = "closed", Roles = new List<string> { "user" } }
                }
            }
        };
        var result = _validator.Validate(spec);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("unknown entity") && e.Message.Contains("NonExistent"));
    }

    // =========================================================================
    // Workflow endpoint coverage
    // =========================================================================

    [Fact]
    public void WorkflowEndpointCoverage_MissingEndpoint_ReturnsError()
    {
        var spec = MakeWorkflowSpec();
        spec.Endpoints.Clear(); // remove all auto-generated endpoints
        var result = _validator.Validate(spec);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("Missing workflow_transition endpoint"));
    }

    [Fact]
    public void WorkflowEndpointCoverage_AllEndpointsPresent_Passes()
    {
        var spec = MakeWorkflowSpec();
        var result = _validator.Validate(spec);
        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.Message)));
    }

    // =========================================================================
    // Permission warnings
    // =========================================================================

    [Fact]
    public void Permission_ManagerRoleBlockedByFilter_ProducesWarning()
    {
        var spec = MakeWorkflowSpec();
        spec.Permissions = new List<PermissionSpec>
        {
            new()
            {
                Entity = "PurchaseRequest",
                Operations = new List<string> { "list", "read", "create", "update", "delete" },
                Filter = "employee_id = auth.user_id"  // no auth.role check
            }
        };

        var result = _validator.Validate(spec);
        // Should still be valid (warnings don't fail) but have a warning
        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.Contains(result.Warnings, w => w.Message.Contains("workflow roles") || w.Message.Contains("filter"));
    }

    [Fact]
    public void Permission_FilterWithAuthRole_NoWarning()
    {
        var spec = MakeWorkflowSpec();
        spec.Permissions = new List<PermissionSpec>
        {
            new()
            {
                Entity = "PurchaseRequest",
                Operations = new List<string> { "list", "read", "create", "update", "delete" },
                Filter = "employee_id = auth.user_id OR auth.role = 'manager'"
            }
        };

        var result = _validator.Validate(spec);
        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.Message)));
        // Should NOT warn since auth.role is already in the filter
        Assert.DoesNotContain(result.Warnings, w =>
            w.Message.Contains("workflow roles") && w.Path.Contains("PurchaseRequest"));
    }

    // =========================================================================
    // JSON string overload
    // =========================================================================

    [Fact]
    public void Validate_ValidJson_Passes()
    {
        var json = """
        {
          "entities": [
            {
              "name": "Task",
              "table": "tasks",
              "fields": [
                { "name": "id", "type": "uuid", "primary": true, "required": true, "unique": false },
                { "name": "title", "type": "string", "primary": false, "required": true, "unique": false }
              ]
            }
          ],
          "endpoints": [
            { "method": "GET", "path": "/tasks", "operation": "list", "entity": "Task", "auth": "authenticated" }
          ],
          "permissions": [],
          "triggers": [],
          "workflows": []
        }
        """;

        var result = _validator.Validate(json);
        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.Message)));
    }

    [Fact]
    public void Validate_MalformedJson_ReturnsFailure()
    {
        var result = _validator.Validate("{ this is not json }");
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("Invalid JSON") || e.Message.Contains("invalid") || e.Message.Contains("JSON"));
    }

    [Fact]
    public void Validate_NullJson_ReturnsFailure()
    {
        var result = _validator.Validate("null");
        Assert.False(result.IsValid);
    }
}
