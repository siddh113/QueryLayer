using System.Text.Json;
using System.Text.RegularExpressions;
using QueryLayer.Api.Models.Runtime;
using QueryLayer.Api.Services.Workflow.Conditions;

namespace QueryLayer.Api.Services.AI;

public class SpecValidator
{
    private readonly ConditionSyntaxValidator _conditionValidator;

    private static readonly HashSet<string> ReservedTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "users", "projects", "project_specs", "project_keys", "auth_users", "platform_users"
    };

    private static readonly HashSet<string> ValidFieldTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "string", "integer", "boolean", "uuid", "timestamp", "text", "decimal"
    };

    private static readonly HashSet<string> ValidMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET", "POST", "PUT", "PATCH", "DELETE"
    };

    private static readonly HashSet<string> ValidOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "list", "read", "create", "update", "delete", "workflow_transition"
    };

    private static readonly HashSet<string> ValidAuthModes = new(StringComparer.OrdinalIgnoreCase)
    {
        "public", "authenticated", "admin", "service"
    };

    private static readonly HashSet<string> ValidTriggerEventOps = new(StringComparer.OrdinalIgnoreCase)
    {
        "created", "updated", "deleted"
    };

    private static readonly HashSet<string> ValidActionTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "webhook", "create", "update", "delay"
    };

    private static readonly Regex DurationRegex = new(@"^\d+(s|m|h)$", RegexOptions.Compiled);
    private static readonly Regex UrlRegex = new(@"^https?://", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PrivateIpRegex = new(
        @"^https?://(localhost|127\.\d+\.\d+\.\d+|10\.\d+\.\d+\.\d+|192\.168\.\d+\.\d+|172\.(1[6-9]|2\d|3[01])\.\d+\.\d+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public SpecValidator(ConditionSyntaxValidator conditionValidator)
    {
        _conditionValidator = conditionValidator;
    }

    public SpecValidationResult Validate(string json)
    {
        BackendSpec spec;
        try
        {
            spec = JsonSerializer.Deserialize<BackendSpec>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            })!;
        }
        catch (JsonException ex)
        {
            return SpecValidationResult.Failure($"Invalid JSON: {ex.Message}");
        }

        if (spec == null)
            return SpecValidationResult.Failure("Deserialized spec is null.");

        return Validate(spec);
    }

    public SpecValidationResult Validate(BackendSpec spec)
    {
        var errors = new List<SpecValidationError>();
        var warnings = new List<SpecValidationError>();

        var entityNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entityTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entityFieldMap = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var tableFieldMap = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        ValidateEntities(spec, errors, warnings, entityNames, entityTables, entityFieldMap, tableFieldMap);
        ValidateEndpoints(spec, errors, warnings, entityNames);
        ValidatePermissions(spec, errors, warnings, entityNames);
        ValidateTriggers(spec, errors, warnings, entityTables, tableFieldMap);
        ValidateWorkflows(spec, errors, warnings, entityNames, entityFieldMap);
        ValidateWorkflowEndpointCoverage(spec, errors);

        return new SpecValidationResult
        {
            IsValid = errors.Count == 0,
            Spec = spec,
            Errors = errors,
            Warnings = warnings
        };
    }

    private void ValidateEntities(
        BackendSpec spec,
        List<SpecValidationError> errors,
        List<SpecValidationError> warnings,
        HashSet<string> entityNames,
        HashSet<string> entityTables,
        Dictionary<string, HashSet<string>> entityFieldMap,
        Dictionary<string, HashSet<string>> tableFieldMap)
    {
        if (spec.Entities == null || spec.Entities.Count == 0)
        {
            errors.Add(new SpecValidationError { Path = "entities", Message = "Spec must have at least one entity." });
            return;
        }

        for (int i = 0; i < spec.Entities.Count; i++)
        {
            var entity = spec.Entities[i];
            var entityPath = $"entities[{i}]";

            if (string.IsNullOrWhiteSpace(entity.Name))
            {
                errors.Add(new SpecValidationError { Path = $"{entityPath}.name", Message = "Entity name is required." });
                continue;
            }

            if (string.IsNullOrWhiteSpace(entity.Table))
                errors.Add(new SpecValidationError { Path = $"{entityPath}.table", Message = $"Entity '{entity.Name}' must have a table name." });
            else if (ReservedTables.Contains(entity.Table))
                errors.Add(new SpecValidationError { Path = $"{entityPath}.table", Message = $"Table name '{entity.Table}' is reserved. Use a different name (e.g., 'app_users', 'members')." });
            else if (!entityTables.Add(entity.Table))
                errors.Add(new SpecValidationError { Path = $"{entityPath}.table", Message = $"Table name '{entity.Table}' is used by multiple entities." });

            if (entity.Fields == null || entity.Fields.Count == 0)
            {
                errors.Add(new SpecValidationError { Path = $"{entityPath}.fields", Message = $"Entity '{entity.Name}' must have at least one field." });
                entityNames.Add(entity.Name);
                continue;
            }

            var primaryCount = entity.Fields.Count(f => f.Primary);
            if (primaryCount == 0)
                errors.Add(new SpecValidationError { Path = $"{entityPath}.fields", Message = $"Entity '{entity.Name}' must have exactly one primary key field." });
            else if (primaryCount > 1)
                errors.Add(new SpecValidationError { Path = $"{entityPath}.fields", Message = $"Entity '{entity.Name}' has {primaryCount} primary key fields; only one is allowed." });

            var fieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var entityFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int j = 0; j < entity.Fields.Count; j++)
            {
                var field = entity.Fields[j];
                var fieldPath = $"{entityPath}.fields[{j}]";

                if (string.IsNullOrWhiteSpace(field.Name))
                {
                    errors.Add(new SpecValidationError { Path = $"{fieldPath}.name", Message = $"Entity '{entity.Name}' has a field with no name." });
                    continue;
                }

                if (!fieldNames.Add(field.Name))
                    errors.Add(new SpecValidationError { Path = $"{fieldPath}.name", Message = $"Entity '{entity.Name}' has duplicate field name '{field.Name}'." });

                if (string.IsNullOrWhiteSpace(field.Type))
                    errors.Add(new SpecValidationError { Path = $"{fieldPath}.type", Message = $"Entity '{entity.Name}', field '{field.Name}' has no type." });
                else if (!ValidFieldTypes.Contains(field.Type))
                    errors.Add(new SpecValidationError { Path = $"{fieldPath}.type", Message = $"Entity '{entity.Name}', field '{field.Name}' has invalid type '{field.Type}'. Allowed: {string.Join(", ", ValidFieldTypes)}." });

                if (field.Relation != null && string.IsNullOrWhiteSpace(field.Relation.Table))
                    errors.Add(new SpecValidationError { Path = $"{fieldPath}.relation.table", Message = $"Entity '{entity.Name}', field '{field.Name}' relation is missing table." });

                entityFields.Add(field.Name);
            }

            entityNames.Add(entity.Name);
            entityFieldMap[entity.Name] = entityFields;
            if (!string.IsNullOrWhiteSpace(entity.Table))
                tableFieldMap[entity.Table] = entityFields;
        }

        // Validate relation cross-references after all entities collected
        for (int i = 0; i < spec.Entities.Count; i++)
        {
            var entity = spec.Entities[i];
            if (entity.Fields == null) continue;
            for (int j = 0; j < entity.Fields.Count; j++)
            {
                var field = entity.Fields[j];
                if (field.Relation == null || string.IsNullOrWhiteSpace(field.Relation.Table)) continue;

                if (!tableFieldMap.TryGetValue(field.Relation.Table, out var refFields))
                {
                    warnings.Add(new SpecValidationError
                    {
                        Path = $"entities[{i}].fields[{j}].relation",
                        Message = $"Entity '{entity.Name}', field '{field.Name}' references unknown table '{field.Relation.Table}'.",
                        Severity = "warning"
                    });
                }
                else if (!string.IsNullOrWhiteSpace(field.Relation.Column) && !refFields.Contains(field.Relation.Column))
                {
                    warnings.Add(new SpecValidationError
                    {
                        Path = $"entities[{i}].fields[{j}].relation.column",
                        Message = $"Entity '{entity.Name}', field '{field.Name}' references unknown column '{field.Relation.Column}' on table '{field.Relation.Table}'.",
                        Severity = "warning"
                    });
                }
            }
        }
    }

    private void ValidateEndpoints(
        BackendSpec spec,
        List<SpecValidationError> errors,
        List<SpecValidationError> warnings,
        HashSet<string> entityNames)
    {
        if (spec.Endpoints == null) return;

        for (int i = 0; i < spec.Endpoints.Count; i++)
        {
            var ep = spec.Endpoints[i];
            var path = $"endpoints[{i}]";

            if (string.IsNullOrWhiteSpace(ep.Method))
                errors.Add(new SpecValidationError { Path = $"{path}.method", Message = "Endpoint method is required." });
            else if (!ValidMethods.Contains(ep.Method))
                errors.Add(new SpecValidationError { Path = $"{path}.method", Message = $"Endpoint has invalid method '{ep.Method}'. Allowed: {string.Join(", ", ValidMethods)}." });

            if (string.IsNullOrWhiteSpace(ep.Path))
                errors.Add(new SpecValidationError { Path = $"{path}.path", Message = "Endpoint path is required." });

            if (string.IsNullOrWhiteSpace(ep.Operation))
                errors.Add(new SpecValidationError { Path = $"{path}.operation", Message = "Endpoint operation is required." });
            else if (!ValidOperations.Contains(ep.Operation))
                errors.Add(new SpecValidationError { Path = $"{path}.operation", Message = $"Endpoint has invalid operation '{ep.Operation}'. Allowed: {string.Join(", ", ValidOperations)}." });

            if (!string.IsNullOrWhiteSpace(ep.Entity) && entityNames.Count > 0 && !entityNames.Contains(ep.Entity))
                errors.Add(new SpecValidationError { Path = $"{path}.entity", Message = $"Endpoint references unknown entity '{ep.Entity}'." });

            if (string.IsNullOrWhiteSpace(ep.Auth))
                errors.Add(new SpecValidationError { Path = $"{path}.auth", Message = $"Endpoint '{ep.Path}' is missing auth mode." });
            else if (!ValidAuthModes.Contains(ep.Auth))
                errors.Add(new SpecValidationError { Path = $"{path}.auth", Message = $"Endpoint has invalid auth mode '{ep.Auth}'. Allowed: {string.Join(", ", ValidAuthModes)}." });

            if (ep.Operation?.Equals("workflow_transition", StringComparison.OrdinalIgnoreCase) == true)
            {
                if (string.IsNullOrWhiteSpace(ep.Transition))
                    errors.Add(new SpecValidationError { Path = $"{path}.transition", Message = $"Endpoint '{ep.Path}' has operation 'workflow_transition' but is missing 'transition' name." });
                else if (spec.Workflows != null)
                {
                    var workflow = spec.Workflows.FirstOrDefault(w =>
                        w.Entity?.Equals(ep.Entity, StringComparison.OrdinalIgnoreCase) == true);
                    if (workflow != null && !workflow.Transitions.Any(t =>
                        t.Name?.Equals(ep.Transition, StringComparison.OrdinalIgnoreCase) == true))
                        errors.Add(new SpecValidationError { Path = $"{path}.transition", Message = $"Endpoint '{ep.Path}' references unknown transition '{ep.Transition}' on entity '{ep.Entity}'." });
                }
            }
        }
    }

    private void ValidatePermissions(
        BackendSpec spec,
        List<SpecValidationError> errors,
        List<SpecValidationError> warnings,
        HashSet<string> entityNames)
    {
        if (spec.Permissions == null) return;

        for (int i = 0; i < spec.Permissions.Count; i++)
        {
            var perm = spec.Permissions[i];
            var path = $"permissions[{i}]";

            if (!string.IsNullOrWhiteSpace(perm.Entity) && entityNames.Count > 0 && !entityNames.Contains(perm.Entity))
                errors.Add(new SpecValidationError { Path = $"{path}.entity", Message = $"Permission references unknown entity '{perm.Entity}'." });

            if (perm.Operations != null)
            {
                for (int j = 0; j < perm.Operations.Count; j++)
                {
                    if (!ValidOperations.Contains(perm.Operations[j]))
                        errors.Add(new SpecValidationError { Path = $"{path}.operations[{j}]", Message = $"Permission has invalid operation '{perm.Operations[j]}'." });
                }
            }
        }

        // Warn if management roles may be blocked by overly restrictive permission filters
        if (spec.Workflows == null) return;

        foreach (var workflow in spec.Workflows)
        {
            var managementRoles = workflow.Transitions
                .SelectMany(t => t.Roles ?? new List<string>())
                .Where(r => !r.Equals("user", StringComparison.OrdinalIgnoreCase) &&
                           !r.Equals("authenticated", StringComparison.OrdinalIgnoreCase) &&
                           !r.Equals("public", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (managementRoles.Count == 0) continue;

            var perm = spec.Permissions?.FirstOrDefault(p =>
                p.Entity?.Equals(workflow.Entity, StringComparison.OrdinalIgnoreCase) == true);

            if (perm != null &&
                !string.IsNullOrWhiteSpace(perm.Filter) &&
                !perm.Filter.Contains("auth.role", StringComparison.OrdinalIgnoreCase) &&
                (perm.Filter.Contains("user_id", StringComparison.OrdinalIgnoreCase) ||
                 perm.Filter.Contains("employee_id", StringComparison.OrdinalIgnoreCase)))
            {
                warnings.Add(new SpecValidationError
                {
                    Path = $"permissions[entity={workflow.Entity}].filter",
                    Message = $"Permission filter may block workflow roles [{string.Join(", ", managementRoles)}]. Consider adding role checks to the filter.",
                    Severity = "warning"
                });
            }
        }
    }

    private void ValidateTriggers(
        BackendSpec spec,
        List<SpecValidationError> errors,
        List<SpecValidationError> warnings,
        HashSet<string> entityTables,
        Dictionary<string, HashSet<string>> tableFieldMap)
    {
        if (spec.Triggers == null) return;

        for (int i = 0; i < spec.Triggers.Count; i++)
        {
            var trigger = spec.Triggers[i];
            var path = $"triggers[{i}]";

            if (string.IsNullOrWhiteSpace(trigger.Name))
                errors.Add(new SpecValidationError { Path = $"{path}.name", Message = "Trigger name is required." });

            if (string.IsNullOrWhiteSpace(trigger.Event))
            {
                errors.Add(new SpecValidationError { Path = $"{path}.event", Message = "Trigger event is required." });
            }
            else
            {
                var parts = trigger.Event.Split('.');
                if (parts.Length != 2)
                {
                    errors.Add(new SpecValidationError { Path = $"{path}.event", Message = $"Trigger event '{trigger.Event}' must be in format 'table.operation' (e.g. 'tasks.created')." });
                }
                else
                {
                    var (table, operation) = (parts[0], parts[1]);
                    if (entityTables.Count > 0 && !entityTables.Contains(table))
                        errors.Add(new SpecValidationError { Path = $"{path}.event", Message = $"Trigger event references unknown table '{table}'." });
                    if (!ValidTriggerEventOps.Contains(operation))
                        errors.Add(new SpecValidationError { Path = $"{path}.event", Message = $"Trigger event has invalid operation '{operation}'. Allowed: created, updated, deleted." });
                }
            }

            if (!string.IsNullOrWhiteSpace(trigger.Condition))
            {
                // Check for SQL injection patterns first
                var injectionThreats = SafeSqlExpressionBuilder.DetectInjectionPatterns(trigger.Condition);
                if (injectionThreats.Count > 0)
                {
                    foreach (var threat in injectionThreats)
                        errors.Add(new SpecValidationError { Path = $"{path}.condition", Message = $"Trigger condition rejected: {threat}" });
                }
                else
                {
                    // Try parsing with the new condition parser for structural validation
                    try
                    {
                        ConditionParser.Parse(trigger.Condition);
                    }
                    catch (ConditionParseException)
                    {
                        // Fall back to legacy regex-based validation if the new parser can't handle it
                        // (e.g., for conditions that use SQL subqueries which will be normalized later)
                    }

                    // Also run the legacy validator for field-level checks
                    var triggerTable = trigger.Event?.Split('.').FirstOrDefault();
                    var fields = triggerTable != null && tableFieldMap.TryGetValue(triggerTable, out var f)
                        ? (IEnumerable<string>)f
                        : Array.Empty<string>();
                    var (condValid, condError) = _conditionValidator.Validate(trigger.Condition, fields);
                    if (!condValid)
                        errors.Add(new SpecValidationError { Path = $"{path}.condition", Message = $"Trigger condition invalid: {condError}" });
                }
            }

            if (trigger.Actions == null || trigger.Actions.Count == 0)
            {
                errors.Add(new SpecValidationError { Path = $"{path}.actions", Message = $"Trigger '{trigger.Name}' must have at least one action." });
            }
            else
            {
                for (int j = 0; j < trigger.Actions.Count; j++)
                    ValidateTriggerAction(trigger.Actions[j], $"{path}.actions[{j}]", trigger.Name, errors, warnings);
            }
        }
    }

    private void ValidateTriggerAction(
        ActionSpec action,
        string path,
        string triggerName,
        List<SpecValidationError> errors,
        List<SpecValidationError> warnings)
    {
        if (string.IsNullOrWhiteSpace(action.Type))
        {
            errors.Add(new SpecValidationError { Path = $"{path}.type", Message = $"Action in trigger '{triggerName}' is missing type." });
            return;
        }

        if (!ValidActionTypes.Contains(action.Type))
        {
            errors.Add(new SpecValidationError { Path = $"{path}.type", Message = $"Action in trigger '{triggerName}' has invalid type '{action.Type}'. Allowed: {string.Join(", ", ValidActionTypes)}." });
            return;
        }

        switch (action.Type.ToLowerInvariant())
        {
            case "webhook":
                if (string.IsNullOrWhiteSpace(action.Url))
                    errors.Add(new SpecValidationError { Path = $"{path}.url", Message = $"Webhook action in trigger '{triggerName}' is missing URL." });
                else if (!UrlRegex.IsMatch(action.Url))
                    errors.Add(new SpecValidationError { Path = $"{path}.url", Message = $"Webhook URL '{action.Url}' must start with http:// or https://." });
                else if (PrivateIpRegex.IsMatch(action.Url))
                    warnings.Add(new SpecValidationError
                    {
                        Path = $"{path}.url",
                        Message = $"Webhook URL '{action.Url}' points to localhost/private IP. Will be blocked in production.",
                        Severity = "warning"
                    });
                break;

            case "create":
                if (string.IsNullOrWhiteSpace(action.Entity))
                    errors.Add(new SpecValidationError { Path = $"{path}.entity", Message = $"Create action in trigger '{triggerName}' is missing entity." });
                if (action.Data == null || action.Data.Count == 0)
                    errors.Add(new SpecValidationError { Path = $"{path}.data", Message = $"Create action in trigger '{triggerName}' is missing data." });
                break;

            case "update":
                if (string.IsNullOrWhiteSpace(action.Entity))
                    errors.Add(new SpecValidationError { Path = $"{path}.entity", Message = $"Update action in trigger '{triggerName}' is missing entity." });
                if (action.Fields == null || action.Fields.Count == 0)
                    errors.Add(new SpecValidationError { Path = $"{path}.fields", Message = $"Update action in trigger '{triggerName}' is missing fields." });
                break;

            case "delay":
                if (string.IsNullOrWhiteSpace(action.Duration))
                    errors.Add(new SpecValidationError { Path = $"{path}.duration", Message = $"Delay action in trigger '{triggerName}' is missing duration." });
                else if (!DurationRegex.IsMatch(action.Duration))
                    errors.Add(new SpecValidationError { Path = $"{path}.duration", Message = $"Delay action duration '{action.Duration}' is invalid. Expected format: 10s, 5m, 1h." });
                break;
        }
    }

    private void ValidateWorkflows(
        BackendSpec spec,
        List<SpecValidationError> errors,
        List<SpecValidationError> warnings,
        HashSet<string> entityNames,
        Dictionary<string, HashSet<string>> entityFieldMap)
    {
        if (spec.Workflows == null) return;

        for (int i = 0; i < spec.Workflows.Count; i++)
        {
            var workflow = spec.Workflows[i];
            var path = $"workflows[{i}]";

            if (string.IsNullOrWhiteSpace(workflow.Entity))
            {
                errors.Add(new SpecValidationError { Path = $"{path}.entity", Message = "Workflow entity is required." });
                continue;
            }

            if (entityNames.Count > 0 && !entityNames.Contains(workflow.Entity))
                errors.Add(new SpecValidationError { Path = $"{path}.entity", Message = $"Workflow references unknown entity '{workflow.Entity}'." });

            if (entityFieldMap.TryGetValue(workflow.Entity, out var fields) && !fields.Contains("state"))
                errors.Add(new SpecValidationError
                {
                    Path = $"{path}.entity",
                    Message = $"Workflow entity '{workflow.Entity}' is missing a 'state' field."
                });

            if (workflow.States == null || workflow.States.Count == 0)
            {
                errors.Add(new SpecValidationError { Path = $"{path}.states", Message = $"Workflow for entity '{workflow.Entity}' must have at least one state." });
                continue;
            }

            var stateSet = new HashSet<string>(workflow.States, StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(workflow.InitialState))
                errors.Add(new SpecValidationError { Path = $"{path}.initialState", Message = $"Workflow for entity '{workflow.Entity}' initialState is required and cannot be empty." });
            else if (!stateSet.Contains(workflow.InitialState))
                errors.Add(new SpecValidationError { Path = $"{path}.initialState", Message = $"Workflow for entity '{workflow.Entity}' initialState '{workflow.InitialState}' is not in the states list." });

            if (workflow.Transitions == null || workflow.Transitions.Count == 0)
            {
                errors.Add(new SpecValidationError { Path = $"{path}.transitions", Message = $"Workflow for entity '{workflow.Entity}' must have at least one transition." });
                continue;
            }

            for (int j = 0; j < workflow.Transitions.Count; j++)
            {
                var transition = workflow.Transitions[j];
                var tPath = $"{path}.transitions[{j}]";

                if (string.IsNullOrWhiteSpace(transition.Name))
                    errors.Add(new SpecValidationError { Path = $"{tPath}.name", Message = $"Transition in workflow '{workflow.Entity}' is missing a name." });

                if (string.IsNullOrWhiteSpace(transition.From))
                    errors.Add(new SpecValidationError { Path = $"{tPath}.from", Message = $"Transition '{transition.Name}' in workflow '{workflow.Entity}' is missing 'from' state." });
                else if (!stateSet.Contains(transition.From))
                    errors.Add(new SpecValidationError { Path = $"{tPath}.from", Message = $"Transition '{transition.Name}' references unknown 'from' state '{transition.From}'." });

                if (string.IsNullOrWhiteSpace(transition.To))
                    errors.Add(new SpecValidationError { Path = $"{tPath}.to", Message = $"Transition '{transition.Name}' in workflow '{workflow.Entity}' is missing 'to' state." });
                else if (!stateSet.Contains(transition.To))
                    errors.Add(new SpecValidationError { Path = $"{tPath}.to", Message = $"Transition '{transition.Name}' references unknown 'to' state '{transition.To}'." });

                if (transition.Roles == null || transition.Roles.Count == 0)
                    errors.Add(new SpecValidationError { Path = $"{tPath}.roles", Message = $"Transition '{transition.Name}' in workflow '{workflow.Entity}' must have at least one role." });

                if (!string.IsNullOrWhiteSpace(transition.Condition))
                {
                    var condFields = fields ?? new HashSet<string>();
                    var (condValid, condError) = _conditionValidator.Validate(transition.Condition, condFields);
                    if (!condValid)
                        errors.Add(new SpecValidationError { Path = $"{tPath}.condition", Message = $"Transition '{transition.Name}' condition invalid: {condError}" });
                }
            }
        }
    }

    private static void ValidateWorkflowEndpointCoverage(
        BackendSpec spec,
        List<SpecValidationError> errors)
    {
        if (spec.Workflows == null || spec.Endpoints == null) return;

        foreach (var workflow in spec.Workflows)
        {
            if (workflow.Transitions == null) continue;

            foreach (var transition in workflow.Transitions)
            {
                if (string.IsNullOrWhiteSpace(transition.Name)) continue;

                var hasEndpoint = spec.Endpoints.Any(ep =>
                    ep.Operation?.Equals("workflow_transition", StringComparison.OrdinalIgnoreCase) == true &&
                    ep.Entity?.Equals(workflow.Entity, StringComparison.OrdinalIgnoreCase) == true &&
                    ep.Transition?.Equals(transition.Name, StringComparison.OrdinalIgnoreCase) == true);

                if (!hasEndpoint)
                    errors.Add(new SpecValidationError
                    {
                        Path = "endpoints",
                        Message = $"Missing workflow_transition endpoint for transition '{transition.Name}' on entity '{workflow.Entity}'."
                    });
            }
        }
    }
}

public class SpecValidationResult
{
    public bool IsValid { get; set; }
    public BackendSpec? Spec { get; set; }
    public List<SpecValidationError> Errors { get; set; } = new();
    public List<SpecValidationError> Warnings { get; set; } = new();

    public string? Error => Errors.Count > 0
        ? string.Join("; ", Errors.Select(e => e.Message))
        : null;

    public static SpecValidationResult Success(BackendSpec spec) =>
        new() { IsValid = true, Spec = spec };

    public static SpecValidationResult Failure(string error) =>
        new() { IsValid = false, Errors = new List<SpecValidationError> { new() { Message = error } } };
}

public class SpecValidationError
{
    public string Path { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = "error";
}
