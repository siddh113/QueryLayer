using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using QueryLayer.Api.Models.Runtime;
using QueryLayer.Api.Services.AI;
using System.Text.Json;
using Xunit;

namespace QueryLayer.Api.Tests;

/// <summary>
/// Tests for SpecRepairService local repairs.
///
/// Strategy: mock AIService so that GenerateWithRetryAsync calls return
/// a controlled JSON string, then inspect the GenerationResult to verify
/// which repairs were applied and that the repaired spec is valid.
/// </summary>
public class SpecRepairServiceTests
{
    // =========================================================================
    // Helpers
    // =========================================================================

    private static SpecRepairService CreateService(Mock<AIService> mockAI)
    {
        var conditionValidator = new ConditionSyntaxValidator();
        var specValidator = new SpecValidator(conditionValidator);
        var logger = NullLogger<SpecRepairService>.Instance;
        return new SpecRepairService(mockAI.Object, specValidator, logger);
    }

    private static Mock<AIService> MockAI(string returnJson)
    {
        var mock = new Mock<AIService>(MockBehavior.Loose,
            new HttpClient(),            // not called — virtual method bypassed
            new PromptBuilder(),
            NullLogger<AIService>.Instance);

        mock.Setup(a => a.GenerateSpecAsync(It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync(returnJson);
        mock.Setup(a => a.RepairSpecAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(returnJson);
        mock.Setup(a => a.EditSpecAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync(returnJson);

        return mock;
    }

    // Serialize a BackendSpec to a JSON string the service can consume
    private static string ToJson(BackendSpec spec) =>
        JsonSerializer.Serialize(spec, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    private static FieldSpec PrimaryField(string name = "id") =>
        new() { Name = name, Type = "uuid", Primary = true, Required = true };

    private static FieldSpec Field(string name, string type = "string", bool required = false) =>
        new() { Name = name, Type = type, Primary = false, Required = required };

    private static BackendSpec BuildSpec(Action<BackendSpec> configure)
    {
        var spec = new BackendSpec();
        configure(spec);
        return spec;
    }

    // =========================================================================
    // Repair 0 — invalid relation reference
    // =========================================================================

    [Fact]
    public async Task Repair0_BadRelation_IsNulledOut()
    {
        var spec = new BackendSpec
        {
            Entities = new List<EntitySpec>
            {
                new()
                {
                    Name = "Task",
                    Table = "tasks",
                    Fields = new List<FieldSpec>
                    {
                        PrimaryField(),
                        new()
                        {
                            Name = "account_id",
                            Type = "uuid",
                            Relation = new RelationSpec { Table = "accounts", Column = "id" }
                        }
                    }
                }
            },
            Endpoints = new List<EndpointSpec>
            {
                new() { Method = "GET", Path = "/tasks", Operation = "list", Entity = "Task", Auth = "authenticated" }
            }
        };

        var mockAI = MockAI(ToJson(spec));
        var service = CreateService(mockAI);

        var result = await service.GenerateWithRetryAsync("test");

        Assert.Contains(result.Repairs, r => r.Message.Contains("accounts") && r.Message.Contains("not found"));
        // After repair the spec should be valid
        Assert.True(result.IsValid, result.Error);
        // The relation on account_id should have been removed
        var taskEntity = result.Spec!.Entities.First(e => e.Name == "Task");
        var accountField = taskEntity.Fields.First(f => f.Name == "account_id");
        Assert.Null(accountField.Relation);
    }

    [Fact]
    public async Task Repair0_ValidRelation_IsKept()
    {
        var spec = new BackendSpec
        {
            Entities = new List<EntitySpec>
            {
                new()
                {
                    Name = "Account",
                    Table = "accounts",
                    Fields = new List<FieldSpec> { PrimaryField() }
                },
                new()
                {
                    Name = "Task",
                    Table = "tasks",
                    Fields = new List<FieldSpec>
                    {
                        PrimaryField(),
                        new()
                        {
                            Name = "account_id",
                            Type = "uuid",
                            Relation = new RelationSpec { Table = "accounts", Column = "id" }
                        }
                    }
                }
            },
            Endpoints = new List<EndpointSpec>
            {
                new() { Method = "GET", Path = "/tasks", Operation = "list", Entity = "Task", Auth = "authenticated" }
            }
        };

        var mockAI = MockAI(ToJson(spec));
        var service = CreateService(mockAI);

        var result = await service.GenerateWithRetryAsync("test");

        var taskEntity = result.Spec!.Entities.First(e => e.Name == "Task");
        var accountField = taskEntity.Fields.First(f => f.Name == "account_id");
        // Relation to a known table must be preserved
        Assert.NotNull(accountField.Relation);
        Assert.Equal("accounts", accountField.Relation.Table, ignoreCase: true);
    }

    // =========================================================================
    // Repair 1 — empty initialState
    // =========================================================================

    [Fact]
    public async Task Repair1_EmptyInitialState_SetToFirstState()
    {
        var spec = new BackendSpec
        {
            Entities = new List<EntitySpec>
            {
                new()
                {
                    Name = "PurchaseRequest",
                    Table = "purchase_requests",
                    Fields = new List<FieldSpec>
                    {
                        PrimaryField(),
                        Field("state", required: true)
                    }
                }
            },
            Endpoints = new List<EndpointSpec>
            {
                new() { Method = "POST", Path = "/purchase_requests/{id}/submit", Operation = "workflow_transition", Entity = "PurchaseRequest", Auth = "authenticated", Transition = "submit" }
            },
            Workflows = new List<WorkflowSpec>
            {
                new()
                {
                    Entity = "PurchaseRequest",
                    States = new List<string> { "draft", "submitted" },
                    InitialState = "",   // <-- invalid — should be repaired
                    Transitions = new List<TransitionSpec>
                    {
                        new() { Name = "submit", From = "draft", To = "submitted", Roles = new List<string> { "employee" } }
                    }
                }
            }
        };

        var mockAI = MockAI(ToJson(spec));
        var service = CreateService(mockAI);

        var result = await service.GenerateWithRetryAsync("test");

        Assert.Contains(result.Repairs, r => r.Path.Contains("initialState") && r.Message.Contains("draft"));
        Assert.Equal("draft", result.Spec!.Workflows[0].InitialState);
    }

    [Fact]
    public async Task Repair1_ValidInitialState_Unchanged()
    {
        var spec = new BackendSpec
        {
            Entities = new List<EntitySpec>
            {
                new()
                {
                    Name = "PurchaseRequest",
                    Table = "purchase_requests",
                    Fields = new List<FieldSpec>
                    {
                        PrimaryField(),
                        Field("state", required: true)
                    }
                }
            },
            Endpoints = new List<EndpointSpec>
            {
                new() { Method = "POST", Path = "/purchase_requests/{id}/submit", Operation = "workflow_transition", Entity = "PurchaseRequest", Auth = "authenticated", Transition = "submit" }
            },
            Workflows = new List<WorkflowSpec>
            {
                new()
                {
                    Entity = "PurchaseRequest",
                    States = new List<string> { "draft", "submitted" },
                    InitialState = "draft",  // already valid
                    Transitions = new List<TransitionSpec>
                    {
                        new() { Name = "submit", From = "draft", To = "submitted", Roles = new List<string> { "employee" } }
                    }
                }
            }
        };

        var mockAI = MockAI(ToJson(spec));
        var service = CreateService(mockAI);

        var result = await service.GenerateWithRetryAsync("test");

        Assert.Equal("draft", result.Spec!.Workflows[0].InitialState);
        Assert.DoesNotContain(result.Repairs, r => r.Path.Contains("initialState"));
    }

    // =========================================================================
    // Repair 2 — missing/wrong state field
    // =========================================================================

    [Fact]
    public async Task Repair2_MissingStateField_StateFieldAdded()
    {
        var spec = new BackendSpec
        {
            Entities = new List<EntitySpec>
            {
                new()
                {
                    Name = "PurchaseRequest",
                    Table = "purchase_requests",
                    Fields = new List<FieldSpec>
                    {
                        PrimaryField(),
                        Field("amount", "decimal")
                        // no state field
                    }
                }
            },
            Endpoints = new List<EndpointSpec>
            {
                new() { Method = "POST", Path = "/purchase_requests/{id}/submit", Operation = "workflow_transition", Entity = "PurchaseRequest", Auth = "authenticated", Transition = "submit" }
            },
            Workflows = new List<WorkflowSpec>
            {
                new()
                {
                    Entity = "PurchaseRequest",
                    States = new List<string> { "draft", "submitted" },
                    InitialState = "draft",
                    Transitions = new List<TransitionSpec>
                    {
                        new() { Name = "submit", From = "draft", To = "submitted", Roles = new List<string> { "employee" } }
                    }
                }
            }
        };

        var mockAI = MockAI(ToJson(spec));
        var service = CreateService(mockAI);

        var result = await service.GenerateWithRetryAsync("test");

        Assert.Contains(result.Repairs, r => r.Message.Contains("state") && r.Message.Contains("PurchaseRequest"));
        var entity = result.Spec!.Entities.First(e => e.Name == "PurchaseRequest");
        Assert.Contains(entity.Fields, f => f.Name == "state" && f.Required);
    }

    [Fact]
    public async Task Repair2_StateFieldNotRequired_SetToRequired()
    {
        var spec = new BackendSpec
        {
            Entities = new List<EntitySpec>
            {
                new()
                {
                    Name = "PurchaseRequest",
                    Table = "purchase_requests",
                    Fields = new List<FieldSpec>
                    {
                        PrimaryField(),
                        new() { Name = "state", Type = "string", Required = false }  // exists but not required
                    }
                }
            },
            Endpoints = new List<EndpointSpec>
            {
                new() { Method = "POST", Path = "/purchase_requests/{id}/submit", Operation = "workflow_transition", Entity = "PurchaseRequest", Auth = "authenticated", Transition = "submit" }
            },
            Workflows = new List<WorkflowSpec>
            {
                new()
                {
                    Entity = "PurchaseRequest",
                    States = new List<string> { "draft", "submitted" },
                    InitialState = "draft",
                    Transitions = new List<TransitionSpec>
                    {
                        new() { Name = "submit", From = "draft", To = "submitted", Roles = new List<string> { "employee" } }
                    }
                }
            }
        };

        var mockAI = MockAI(ToJson(spec));
        var service = CreateService(mockAI);

        var result = await service.GenerateWithRetryAsync("test");

        Assert.Contains(result.Repairs, r => r.Message.Contains("required=true") || r.Message.Contains("required"));
        var entity = result.Spec!.Entities.First(e => e.Name == "PurchaseRequest");
        var stateField = entity.Fields.First(f => f.Name == "state");
        Assert.True(stateField.Required);
    }

    [Fact]
    public async Task Repair2_StateFieldAlreadyRequired_NoRepair()
    {
        var spec = new BackendSpec
        {
            Entities = new List<EntitySpec>
            {
                new()
                {
                    Name = "PurchaseRequest",
                    Table = "purchase_requests",
                    Fields = new List<FieldSpec>
                    {
                        PrimaryField(),
                        new() { Name = "state", Type = "string", Required = true }  // already correct
                    }
                }
            },
            Endpoints = new List<EndpointSpec>
            {
                new() { Method = "POST", Path = "/purchase_requests/{id}/submit", Operation = "workflow_transition", Entity = "PurchaseRequest", Auth = "authenticated", Transition = "submit" }
            },
            Workflows = new List<WorkflowSpec>
            {
                new()
                {
                    Entity = "PurchaseRequest",
                    States = new List<string> { "draft", "submitted" },
                    InitialState = "draft",
                    Transitions = new List<TransitionSpec>
                    {
                        new() { Name = "submit", From = "draft", To = "submitted", Roles = new List<string> { "employee" } }
                    }
                }
            }
        };

        var mockAI = MockAI(ToJson(spec));
        var service = CreateService(mockAI);

        var result = await service.GenerateWithRetryAsync("test");

        // No repair for state field when it's already correct
        Assert.DoesNotContain(result.Repairs, r => r.Message.Contains("required=true") && r.Path.Contains("state"));
    }

    // =========================================================================
    // Repair 3 — missing transition endpoints
    // =========================================================================

    [Fact]
    public async Task Repair3_MissingTransitionEndpoint_EndpointAdded()
    {
        var spec = new BackendSpec
        {
            Entities = new List<EntitySpec>
            {
                new()
                {
                    Name = "PurchaseRequest",
                    Table = "purchase_requests",
                    Fields = new List<FieldSpec>
                    {
                        PrimaryField(),
                        new() { Name = "state", Type = "string", Required = true }
                    }
                }
            },
            Endpoints = new List<EndpointSpec>(), // <-- no endpoints at all
            Workflows = new List<WorkflowSpec>
            {
                new()
                {
                    Entity = "PurchaseRequest",
                    States = new List<string> { "draft", "submitted" },
                    InitialState = "draft",
                    Transitions = new List<TransitionSpec>
                    {
                        new() { Name = "submit", From = "draft", To = "submitted", Roles = new List<string> { "employee" } }
                    }
                }
            }
        };

        var mockAI = MockAI(ToJson(spec));
        var service = CreateService(mockAI);

        var result = await service.GenerateWithRetryAsync("test");

        Assert.Contains(result.Repairs, r => r.Message.Contains("submit") && r.Path == "endpoints");
        Assert.Contains(result.Spec!.Endpoints, ep =>
            ep.Operation == "workflow_transition" &&
            ep.Entity == "PurchaseRequest" &&
            ep.Transition == "submit");
    }

    [Fact]
    public async Task Repair3_EndpointAlreadyExists_NoDuplicate()
    {
        var spec = new BackendSpec
        {
            Entities = new List<EntitySpec>
            {
                new()
                {
                    Name = "PurchaseRequest",
                    Table = "purchase_requests",
                    Fields = new List<FieldSpec>
                    {
                        PrimaryField(),
                        new() { Name = "state", Type = "string", Required = true }
                    }
                }
            },
            Endpoints = new List<EndpointSpec>
            {
                new()
                {
                    Method = "POST",
                    Path = "/purchase_requests/{id}/submit",
                    Operation = "workflow_transition",
                    Entity = "PurchaseRequest",
                    Auth = "authenticated",
                    Transition = "submit"
                }
            },
            Workflows = new List<WorkflowSpec>
            {
                new()
                {
                    Entity = "PurchaseRequest",
                    States = new List<string> { "draft", "submitted" },
                    InitialState = "draft",
                    Transitions = new List<TransitionSpec>
                    {
                        new() { Name = "submit", From = "draft", To = "submitted", Roles = new List<string> { "employee" } }
                    }
                }
            }
        };

        var mockAI = MockAI(ToJson(spec));
        var service = CreateService(mockAI);

        var result = await service.GenerateWithRetryAsync("test");

        var submitEndpoints = result.Spec!.Endpoints
            .Where(ep => ep.Operation == "workflow_transition" && ep.Transition == "submit")
            .ToList();

        // Must have exactly one, not duplicated
        Assert.Single(submitEndpoints);
    }

    // =========================================================================
    // Repair 4 — manager permission expansion
    // =========================================================================

    [Fact]
    public async Task Repair4_ManagerRoleBlockedByFilter_FilterExpanded()
    {
        var spec = new BackendSpec
        {
            Entities = new List<EntitySpec>
            {
                new()
                {
                    Name = "PurchaseRequest",
                    Table = "purchase_requests",
                    Fields = new List<FieldSpec>
                    {
                        PrimaryField(),
                        new() { Name = "state", Type = "string", Required = true }
                    }
                }
            },
            Endpoints = new List<EndpointSpec>
            {
                new() { Method = "POST", Path = "/purchase_requests/{id}/approve", Operation = "workflow_transition", Entity = "PurchaseRequest", Auth = "authenticated", Transition = "approve" }
            },
            Permissions = new List<PermissionSpec>
            {
                new()
                {
                    Entity = "PurchaseRequest",
                    Operations = new List<string> { "list", "read", "create", "update", "delete" },
                    Filter = "employee_id = auth.user_id"   // blocks manager
                }
            },
            Workflows = new List<WorkflowSpec>
            {
                new()
                {
                    Entity = "PurchaseRequest",
                    States = new List<string> { "draft", "submitted", "approved" },
                    InitialState = "draft",
                    Transitions = new List<TransitionSpec>
                    {
                        new() { Name = "approve", From = "submitted", To = "approved", Roles = new List<string> { "manager" } }
                    }
                }
            }
        };

        var mockAI = MockAI(ToJson(spec));
        var service = CreateService(mockAI);

        var result = await service.GenerateWithRetryAsync("test");

        Assert.Contains(result.Repairs, r => r.Message.Contains("manager") || r.Message.Contains("filter"));
        var perm = result.Spec!.Permissions.First(p => p.Entity == "PurchaseRequest");
        Assert.Contains("auth.role", perm.Filter, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Repair4_FilterAlreadyHasAuthRole_NotModified()
    {
        var originalFilter = "employee_id = auth.user_id OR auth.role = 'manager'";
        var spec = new BackendSpec
        {
            Entities = new List<EntitySpec>
            {
                new()
                {
                    Name = "PurchaseRequest",
                    Table = "purchase_requests",
                    Fields = new List<FieldSpec>
                    {
                        PrimaryField(),
                        new() { Name = "state", Type = "string", Required = true }
                    }
                }
            },
            Endpoints = new List<EndpointSpec>
            {
                new() { Method = "POST", Path = "/purchase_requests/{id}/approve", Operation = "workflow_transition", Entity = "PurchaseRequest", Auth = "authenticated", Transition = "approve" }
            },
            Permissions = new List<PermissionSpec>
            {
                new()
                {
                    Entity = "PurchaseRequest",
                    Operations = new List<string> { "list", "read", "create", "update", "delete" },
                    Filter = originalFilter    // already has auth.role
                }
            },
            Workflows = new List<WorkflowSpec>
            {
                new()
                {
                    Entity = "PurchaseRequest",
                    States = new List<string> { "draft", "submitted", "approved" },
                    InitialState = "draft",
                    Transitions = new List<TransitionSpec>
                    {
                        new() { Name = "approve", From = "submitted", To = "approved", Roles = new List<string> { "manager" } }
                    }
                }
            }
        };

        var mockAI = MockAI(ToJson(spec));
        var service = CreateService(mockAI);

        var result = await service.GenerateWithRetryAsync("test");

        var perm = result.Spec!.Permissions.First(p => p.Entity == "PurchaseRequest");
        // Filter should not be double-modified — auth.role should only appear once in concept
        Assert.Equal(originalFilter, perm.Filter);
    }

    // =========================================================================
    // Repair 6 — missing reject transition when rejected state exists
    // =========================================================================

    [Fact]
    public async Task Repair6_RejectedStateWithoutTransition_TransitionAndEndpointAdded()
    {
        var spec = new BackendSpec
        {
            Entities = new List<EntitySpec>
            {
                new()
                {
                    Name = "PurchaseRequest",
                    Table = "purchase_requests",
                    Fields = new List<FieldSpec>
                    {
                        PrimaryField(),
                        new() { Name = "state", Type = "string", Required = true }
                    }
                }
            },
            Endpoints = new List<EndpointSpec>
            {
                new() { Method = "POST", Path = "/purchase_requests/{id}/approve", Operation = "workflow_transition", Entity = "PurchaseRequest", Auth = "authenticated", Transition = "approve" }
            },
            Workflows = new List<WorkflowSpec>
            {
                new()
                {
                    Entity = "PurchaseRequest",
                    States = new List<string> { "draft", "submitted", "approved", "rejected" }, // rejected exists
                    InitialState = "draft",
                    Transitions = new List<TransitionSpec>
                    {
                        new() { Name = "approve", From = "submitted", To = "approved", Roles = new List<string> { "manager" } }
                        // no reject transition
                    }
                }
            }
        };

        var mockAI = MockAI(ToJson(spec));
        var service = CreateService(mockAI);

        var result = await service.GenerateWithRetryAsync("test");

        Assert.Contains(result.Repairs, r => r.Message.Contains("reject") && r.Message.Contains("transition"));
        Assert.Contains(result.Spec!.Workflows[0].Transitions, t => t.To == "rejected");
        Assert.Contains(result.Spec!.Endpoints, ep =>
            ep.Operation == "workflow_transition" && ep.Transition == "reject");
    }

    [Fact]
    public async Task Repair6_RejectedStateWithExistingTransition_NothingAdded()
    {
        var spec = new BackendSpec
        {
            Entities = new List<EntitySpec>
            {
                new()
                {
                    Name = "PurchaseRequest",
                    Table = "purchase_requests",
                    Fields = new List<FieldSpec>
                    {
                        PrimaryField(),
                        new() { Name = "state", Type = "string", Required = true }
                    }
                }
            },
            Endpoints = new List<EndpointSpec>
            {
                new() { Method = "POST", Path = "/purchase_requests/{id}/approve", Operation = "workflow_transition", Entity = "PurchaseRequest", Auth = "authenticated", Transition = "approve" },
                new() { Method = "POST", Path = "/purchase_requests/{id}/reject", Operation = "workflow_transition", Entity = "PurchaseRequest", Auth = "authenticated", Transition = "reject" }
            },
            Workflows = new List<WorkflowSpec>
            {
                new()
                {
                    Entity = "PurchaseRequest",
                    States = new List<string> { "draft", "submitted", "approved", "rejected" },
                    InitialState = "draft",
                    Transitions = new List<TransitionSpec>
                    {
                        new() { Name = "approve", From = "submitted", To = "approved", Roles = new List<string> { "manager" } },
                        new() { Name = "reject", From = "submitted", To = "rejected", Roles = new List<string> { "manager" } } // already exists
                    }
                }
            }
        };

        var mockAI = MockAI(ToJson(spec));
        var service = CreateService(mockAI);

        var result = await service.GenerateWithRetryAsync("test");

        var rejectTransitions = result.Spec!.Workflows[0].Transitions
            .Where(t => t.To == "rejected")
            .ToList();

        Assert.Single(rejectTransitions); // not duplicated
    }

    // =========================================================================
    // NormalizeJsonKeys — snake_case to camelCase
    // =========================================================================

    [Fact]
    public async Task NormalizeJsonKeys_SnakeCaseInitialState_Parsed()
    {
        // Inject raw JSON with snake_case keys — the service should normalize them
        var json = """
        {
          "entities": [
            {
              "name": "PurchaseRequest",
              "table": "purchase_requests",
              "fields": [
                { "name": "id", "type": "uuid", "primary": true, "required": true, "unique": false },
                { "name": "state", "type": "string", "primary": false, "required": true, "unique": false }
              ]
            }
          ],
          "endpoints": [
            { "method": "POST", "path": "/purchase_requests/{id}/submit", "operation": "workflow_transition", "entity": "PurchaseRequest", "auth": "authenticated", "transition": "submit" }
          ],
          "permissions": [],
          "triggers": [],
          "workflows": [
            {
              "entity": "PurchaseRequest",
              "states": ["draft", "submitted"],
              "initial_state": "draft",
              "transitions": [
                { "name": "submit", "from": "draft", "to": "submitted", "roles": ["employee"] }
              ]
            }
          ]
        }
        """;

        var mockAI = MockAI(json);
        var service = CreateService(mockAI);

        var result = await service.GenerateWithRetryAsync("test");

        Assert.True(result.IsValid, result.Error ?? "no error");
        Assert.Equal("draft", result.Spec!.Workflows[0].InitialState);
    }

    // =========================================================================
    // GenerationResult structure
    // =========================================================================

    [Fact]
    public async Task GenerationResult_ValidSpec_IsValidTrue()
    {
        var spec = new BackendSpec
        {
            Entities = new List<EntitySpec>
            {
                new()
                {
                    Name = "Task",
                    Table = "tasks",
                    Fields = new List<FieldSpec>
                    {
                        PrimaryField(),
                        Field("title", required: true)
                    }
                }
            },
            Endpoints = new List<EndpointSpec>
            {
                new() { Method = "GET", Path = "/tasks", Operation = "list", Entity = "Task", Auth = "authenticated" }
            }
        };

        var mockAI = MockAI(ToJson(spec));
        var service = CreateService(mockAI);

        var result = await service.GenerateWithRetryAsync("simple task app");

        Assert.True(result.IsValid);
        Assert.NotNull(result.Spec);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task EditWithRetryAsync_ValidSpec_ReturnsValidResult()
    {
        var spec = new BackendSpec
        {
            Entities = new List<EntitySpec>
            {
                new()
                {
                    Name = "Task",
                    Table = "tasks",
                    Fields = new List<FieldSpec>
                    {
                        PrimaryField(),
                        Field("title", required: true)
                    }
                }
            },
            Endpoints = new List<EndpointSpec>
            {
                new() { Method = "GET", Path = "/tasks", Operation = "list", Entity = "Task", Auth = "authenticated" }
            }
        };

        var mockAI = MockAI(ToJson(spec));
        var service = CreateService(mockAI);

        var result = await service.EditWithRetryAsync(ToJson(spec), "add due_date field");

        Assert.True(result.IsValid);
        Assert.NotNull(result.Spec);
    }
}
