using System.Text.RegularExpressions;
using QueryLayer.Api.Models.Runtime;
using QueryLayer.Api.Services.Workflow.Conditions;

namespace QueryLayer.Api.Services.AI;

public class SpecRepairService
{
    private readonly AIService _aiService;
    private readonly SpecValidator _specValidator;
    private readonly ConditionNormalizationService _conditionNormalizer;
    private readonly RelatedRecordResolver _relatedResolver;
    private readonly ILogger<SpecRepairService> _logger;

    private static readonly string[] WorkflowIntentKeywords =
        { "approve", "approval", "workflow", "state machine", "submit", "reject", "manager", "finance" };

    private static readonly string[] TriggerIntentKeywords =
        { "notify", "notification", "webhook", "when ", "after ", "trigger", "send", "on approval" };

    private static readonly string[] NumericFieldNames =
        { "amount", "total", "value", "price", "cost", "sum", "balance", "quantity" };

    private static readonly Regex ThresholdAboveRegex = new(
        @"(?:above|over|greater than|more than|exceeds?)\s*\$?\s*(\d+(?:\.\d+)?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ThresholdBelowRegex = new(
        @"(?:below|under|less than|fewer than)\s*\$?\s*(\d+(?:\.\d+)?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public SpecRepairService(
        AIService aiService,
        SpecValidator specValidator,
        ConditionNormalizationService conditionNormalizer,
        RelatedRecordResolver relatedResolver,
        ILogger<SpecRepairService> logger)
    {
        _aiService = aiService;
        _specValidator = specValidator;
        _conditionNormalizer = conditionNormalizer;
        _relatedResolver = relatedResolver;
        _logger = logger;
    }

    public async Task<GenerationResult> GenerateWithRetryAsync(string userPrompt)
    {
        var allRepairs = new List<RepairAction>();

        // Round 1: generate + local repair
        var json = await _aiService.GenerateSpecAsync(userPrompt);
        var result = TryLocalRepairAndValidate(json, userPrompt, allRepairs);

        if (!result.IsValid)
        {
            _logger.LogWarning("Initial generation failed validation: {Error}", result.Error);

            // If workflow/trigger intent detected but AI returned empty arrays, escalate immediately
            if (result.Spec != null)
            {
                var needsRegen = (HasWorkflowIntent(userPrompt) && result.Spec.Workflows.Count == 0) ||
                                 (HasTriggerIntent(userPrompt) && result.Spec.Triggers.Count == 0);
                if (needsRegen)
                {
                    _logger.LogWarning("Workflow/trigger intent detected but AI returned empty arrays. Escalating to stronger model.");
                    json = await _aiService.GenerateSpecAsync(userPrompt, useStrongerModel: true);
                    result = TryLocalRepairAndValidate(json, userPrompt, allRepairs);
                    if (result.IsValid)
                        return new GenerationResult { Validation = result, Repairs = allRepairs };
                }
            }

            // Round 2: AI repair
            json = await _aiService.RepairSpecAsync(json, result.Error!, userPrompt);
            result = TryLocalRepairAndValidate(json, userPrompt, allRepairs);

            if (!result.IsValid)
            {
                _logger.LogWarning("Repair attempt failed: {Error}. Escalating to stronger model.", result.Error);

                // Round 3: stronger model
                json = await _aiService.GenerateSpecAsync(userPrompt, useStrongerModel: true);
                result = TryLocalRepairAndValidate(json, userPrompt, allRepairs);

                if (!result.IsValid)
                    _logger.LogError("All generation attempts failed. Last error: {Error}", result.Error);
            }
        }

        return new GenerationResult { Validation = result, Repairs = allRepairs };
    }

    public async Task<GenerationResult> EditWithRetryAsync(string currentSpec, string instruction)
    {
        var allRepairs = new List<RepairAction>();

        // Round 1: edit + local repair
        var json = await _aiService.EditSpecAsync(currentSpec, instruction);
        var result = TryLocalRepairAndValidate(json, "", allRepairs);

        if (!result.IsValid)
        {
            _logger.LogWarning("Initial edit failed validation: {Error}", result.Error);

            // Round 2: AI repair
            json = await _aiService.RepairSpecAsync(json, result.Error!, "");
            result = TryLocalRepairAndValidate(json, "", allRepairs);

            if (!result.IsValid)
            {
                _logger.LogWarning("Repair attempt failed: {Error}. Escalating to stronger model.", result.Error);

                // Round 3: stronger model
                json = await _aiService.EditSpecAsync(currentSpec, instruction, useStrongerModel: true);
                result = TryLocalRepairAndValidate(json, "", allRepairs);

                if (!result.IsValid)
                    _logger.LogError("All edit attempts failed. Last error: {Error}", result.Error);
            }
        }

        return new GenerationResult { Validation = result, Repairs = allRepairs };
    }

    private SpecValidationResult TryLocalRepairAndValidate(string json, string userPrompt, List<RepairAction> allRepairs)
    {
        json = NormalizeJsonKeys(json);
        var initial = _specValidator.Validate(json);

        if (initial.Spec == null)
            return initial; // JSON parse failed — cannot repair

        var (repairedSpec, repairs) = LocalRepair(initial.Spec, userPrompt);
        allRepairs.AddRange(repairs);

        return _specValidator.Validate(repairedSpec);
    }

    private (BackendSpec spec, List<RepairAction> repairs) LocalRepair(BackendSpec spec, string userPrompt)
    {
        var repairs = new List<RepairAction>();

        // Repair 0: invalid relation references (table not found in spec)
        var existingTables = spec.Entities
            .Where(e => !string.IsNullOrWhiteSpace(e.Table))
            .Select(e => e.Table!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var entity in spec.Entities)
        {
            foreach (var field in entity.Fields)
            {
                if (field.Relation != null && !string.IsNullOrWhiteSpace(field.Relation.Table))
                {
                    if (!existingTables.Contains(field.Relation.Table))
                    {
                        var badTable = field.Relation.Table;
                        field.Relation = null;
                        repairs.Add(new RepairAction
                        {
                            Path = $"entities[name={entity.Name}].fields[name={field.Name}].relation",
                            Message = $"Removed invalid relation on field '{field.Name}' — table '{badTable}' not found in spec"
                        });
                    }
                }
            }
        }

        // Repair 1: empty initialState
        for (int i = 0; i < spec.Workflows.Count; i++)
        {
            var workflow = spec.Workflows[i];
            if (string.IsNullOrWhiteSpace(workflow.InitialState) && workflow.States.Count > 0)
            {
                workflow.InitialState = workflow.States[0];
                repairs.Add(new RepairAction
                {
                    Path = $"workflows[{i}].initialState",
                    Message = $"Set initialState to first workflow state: {workflow.InitialState}"
                });
            }
        }

        // Repair 2: missing state field on workflow entities, and ensure existing state field is required=true
        for (int i = 0; i < spec.Workflows.Count; i++)
        {
            var workflow = spec.Workflows[i];
            var entity = spec.Entities.FirstOrDefault(e =>
                e.Name.Equals(workflow.Entity, StringComparison.OrdinalIgnoreCase));

            if (entity == null) continue;

            var existingStateField = entity.Fields.FirstOrDefault(f =>
                f.Name.Equals("state", StringComparison.OrdinalIgnoreCase));

            if (existingStateField == null)
            {
                entity.Fields.Add(new FieldSpec
                {
                    Name = "state",
                    Type = "string",
                    Primary = false,
                    Required = true,
                    Unique = false
                });
                repairs.Add(new RepairAction
                {
                    Path = $"entities[name={entity.Name}].fields",
                    Message = $"Added missing 'state' field to entity '{entity.Name}'"
                });
            }
            else if (!existingStateField.Required)
            {
                existingStateField.Required = true;
                repairs.Add(new RepairAction
                {
                    Path = $"entities[name={entity.Name}].fields[name=state].required",
                    Message = $"Set state field required=true on entity '{entity.Name}' (workflow entity state must always be present)"
                });
            }
        }

        // Repair 3: missing workflow transition endpoints
        for (int i = 0; i < spec.Workflows.Count; i++)
        {
            var workflow = spec.Workflows[i];
            var entity = spec.Entities.FirstOrDefault(e =>
                e.Name.Equals(workflow.Entity, StringComparison.OrdinalIgnoreCase));
            if (entity == null) continue;

            foreach (var transition in workflow.Transitions)
            {
                if (string.IsNullOrWhiteSpace(transition.Name)) continue;

                var hasEndpoint = spec.Endpoints.Any(ep =>
                    ep.Operation?.Equals("workflow_transition", StringComparison.OrdinalIgnoreCase) == true &&
                    ep.Entity?.Equals(workflow.Entity, StringComparison.OrdinalIgnoreCase) == true &&
                    ep.Transition?.Equals(transition.Name, StringComparison.OrdinalIgnoreCase) == true);

                if (!hasEndpoint)
                {
                    spec.Endpoints.Add(new EndpointSpec
                    {
                        Method = "POST",
                        Path = $"/{entity.Table}/{{id}}/{transition.Name}",
                        Operation = "workflow_transition",
                        Entity = workflow.Entity,
                        Auth = "authenticated",
                        Transition = transition.Name
                    });
                    repairs.Add(new RepairAction
                    {
                        Path = "endpoints",
                        Message = $"Added missing workflow_transition endpoint POST /{entity.Table}/{{id}}/{transition.Name}"
                    });
                }
            }
        }

        // Repair 4: manager role blocked by permissions
        for (int i = 0; i < spec.Workflows.Count; i++)
        {
            var workflow = spec.Workflows[i];
            var managementRoles = workflow.Transitions
                .SelectMany(t => t.Roles ?? new List<string>())
                .Where(r => !r.Equals("user", StringComparison.OrdinalIgnoreCase) &&
                           !r.Equals("authenticated", StringComparison.OrdinalIgnoreCase) &&
                           !r.Equals("public", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Always ensure admin can access entities with management workflows
            if (!managementRoles.Any(r => r.Equals("admin", StringComparison.OrdinalIgnoreCase)))
                managementRoles.Add("admin");

            if (managementRoles.Count == 0) continue;

            var perm = spec.Permissions.FirstOrDefault(p =>
                p.Entity?.Equals(workflow.Entity, StringComparison.OrdinalIgnoreCase) == true);

            if (perm != null &&
                !string.IsNullOrWhiteSpace(perm.Filter) &&
                !perm.Filter.Contains("auth.role", StringComparison.OrdinalIgnoreCase) &&
                (perm.Filter.Contains("user_id", StringComparison.OrdinalIgnoreCase) ||
                 perm.Filter.Contains("employee_id", StringComparison.OrdinalIgnoreCase)))
            {
                var roleConditions = string.Join(" OR ", managementRoles.Select(r => $"auth.role = '{r}'"));
                perm.Filter = $"{perm.Filter} OR {roleConditions}";
                repairs.Add(new RepairAction
                {
                    Path = $"permissions[entity={workflow.Entity}].filter",
                    Message = $"Expanded permission filter to allow workflow roles: {string.Join(", ", managementRoles)}"
                });
            }
        }

        // Repair 5: missing threshold conditions on approval transitions
        if (!string.IsNullOrWhiteSpace(userPrompt))
        {
            var aboveMatch = ThresholdAboveRegex.Match(userPrompt);
            var belowMatch = ThresholdBelowRegex.Match(userPrompt);

            if (aboveMatch.Success || belowMatch.Success)
            {
                foreach (var workflow in spec.Workflows)
                {
                    var entity = spec.Entities.FirstOrDefault(e =>
                        e.Name.Equals(workflow.Entity, StringComparison.OrdinalIgnoreCase));

                    var numericField = entity?.Fields.FirstOrDefault(f =>
                        NumericFieldNames.Any(n => n.Equals(f.Name, StringComparison.OrdinalIgnoreCase)) &&
                        (f.Type == "decimal" || f.Type == "integer"))?.Name;

                    if (numericField == null) continue;

                    foreach (var transition in workflow.Transitions)
                    {
                        var isApprovalType =
                            transition.Name.Contains("approve", StringComparison.OrdinalIgnoreCase) ||
                            transition.Name.Contains("reject", StringComparison.OrdinalIgnoreCase);

                        if (!isApprovalType || !string.IsNullOrWhiteSpace(transition.Condition)) continue;

                        if (aboveMatch.Success)
                        {
                            transition.Condition = $"{numericField} > {aboveMatch.Groups[1].Value}";
                            repairs.Add(new RepairAction
                            {
                                Path = $"workflows[entity={workflow.Entity}].transitions[{transition.Name}].condition",
                                Message = $"Added threshold condition from prompt: {transition.Condition}"
                            });
                        }
                        else if (belowMatch.Success)
                        {
                            transition.Condition = $"{numericField} < {belowMatch.Groups[1].Value}";
                            repairs.Add(new RepairAction
                            {
                                Path = $"workflows[entity={workflow.Entity}].transitions[{transition.Name}].condition",
                                Message = $"Added threshold condition from prompt: {transition.Condition}"
                            });
                        }
                    }
                }
            }
        }

        // Repair 6: missing reject transition when "rejected" state exists
        for (int i = 0; i < spec.Workflows.Count; i++)
        {
            var workflow = spec.Workflows[i];

            var hasRejectedState = workflow.States.Any(s =>
                s.Equals("rejected", StringComparison.OrdinalIgnoreCase));

            if (!hasRejectedState) continue;

            var hasRejectTransition = workflow.Transitions.Any(t =>
                t.To?.Equals("rejected", StringComparison.OrdinalIgnoreCase) == true);

            if (hasRejectTransition) continue;

            // Pick "from" state: prefer "submitted", otherwise use first non-initial state
            var fromState = workflow.Transitions
                .Select(t => t.From)
                .FirstOrDefault(s => s?.Equals("submitted", StringComparison.OrdinalIgnoreCase) == true)
                ?? workflow.States.FirstOrDefault(s =>
                    !s.Equals(workflow.InitialState, StringComparison.OrdinalIgnoreCase) &&
                    !s.Equals("rejected", StringComparison.OrdinalIgnoreCase))
                ?? workflow.States.FirstOrDefault();

            var entity = spec.Entities.FirstOrDefault(e =>
                e.Name.Equals(workflow.Entity, StringComparison.OrdinalIgnoreCase));

            // Compute threshold condition so the new reject transition matches Repair 5 behavior
            string? rejectCondition = null;
            if (!string.IsNullOrWhiteSpace(userPrompt) && entity != null)
            {
                var numericField = entity.Fields.FirstOrDefault(f =>
                    NumericFieldNames.Any(n => n.Equals(f.Name, StringComparison.OrdinalIgnoreCase)) &&
                    (f.Type == "decimal" || f.Type == "integer"))?.Name;

                if (numericField != null)
                {
                    var aboveMatch = ThresholdAboveRegex.Match(userPrompt);
                    if (aboveMatch.Success)
                        rejectCondition = $"{numericField} > {aboveMatch.Groups[1].Value}";
                }
            }

            workflow.Transitions.Add(new TransitionSpec
            {
                Name = "reject",
                From = fromState ?? "submitted",
                To = "rejected",
                Roles = new List<string> { "manager" },
                Condition = rejectCondition
            });

            if (entity != null)
            {
                var hasRejectEndpoint = spec.Endpoints.Any(ep =>
                    ep.Operation?.Equals("workflow_transition", StringComparison.OrdinalIgnoreCase) == true &&
                    ep.Entity?.Equals(workflow.Entity, StringComparison.OrdinalIgnoreCase) == true &&
                    ep.Transition?.Equals("reject", StringComparison.OrdinalIgnoreCase) == true);

                if (!hasRejectEndpoint)
                {
                    spec.Endpoints.Add(new EndpointSpec
                    {
                        Method = "POST",
                        Path = $"/{entity.Table}/{{id}}/reject",
                        Operation = "workflow_transition",
                        Entity = workflow.Entity,
                        Auth = "authenticated",
                        Transition = "reject"
                    });
                }
            }

            repairs.Add(new RepairAction
            {
                Path = $"workflows[{i}].transitions",
                Message = $"Added missing 'reject' transition and endpoint for entity '{workflow.Entity}' (rejected state exists without transition)"
            });
        }

        // Repair 7: Add missing 'where' to update trigger actions targeting non-current entity
        RepairMissingUpdateActionWhere(spec, repairs);

        // Repair 8: Normalize SQL subqueries in trigger conditions
        RepairNormalizeTriggerConditions(spec, repairs);

        return (spec, repairs);
    }

    private void RepairMissingUpdateActionWhere(BackendSpec spec, List<RepairAction> repairs)
    {
        foreach (var trigger in spec.Triggers)
        {
            var triggerTable = trigger.Event?.Split('.').FirstOrDefault();
            if (string.IsNullOrWhiteSpace(triggerTable)) continue;

            var triggerEntity = spec.Entities.FirstOrDefault(e =>
                e.Table.Equals(triggerTable, StringComparison.OrdinalIgnoreCase));
            if (triggerEntity == null) continue;

            for (int j = 0; j < trigger.Actions.Count; j++)
            {
                var action = trigger.Actions[j];
                if (!action.Type.Equals("update", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (string.IsNullOrWhiteSpace(action.Entity))
                    continue;

                // Skip if action targets the same entity as the trigger
                if (action.Entity.Equals(triggerEntity.Name, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Skip if where is already set
                if (!string.IsNullOrWhiteSpace(action.Where))
                    continue;

                // Find the target entity
                var targetEntity = spec.Entities.FirstOrDefault(e =>
                    e.Name.Equals(action.Entity, StringComparison.OrdinalIgnoreCase));
                if (targetEntity == null) continue;

                // Look for a foreign key from the trigger entity to the target entity
                var fkField = triggerEntity.Fields.FirstOrDefault(f =>
                    f.Relation != null &&
                    f.Relation.Table.Equals(targetEntity.Table, StringComparison.OrdinalIgnoreCase));

                if (fkField != null)
                {
                    var relatedColumn = string.IsNullOrWhiteSpace(fkField.Relation!.Column) ? "id" : fkField.Relation.Column;
                    action.Where = $"{relatedColumn} == record.{fkField.Name}";
                    repairs.Add(new RepairAction
                    {
                        Path = $"triggers[name={trigger.Name}].actions[{j}].where",
                        Message = $"Added missing 'where' clause to update action targeting '{action.Entity}': {action.Where}"
                    });
                }
            }
        }
    }

    private void RepairNormalizeTriggerConditions(BackendSpec spec, List<RepairAction> repairs)
    {
        foreach (var trigger in spec.Triggers)
        {
            if (string.IsNullOrWhiteSpace(trigger.Condition))
                continue;

            // Only try normalization if the condition contains SELECT (likely raw SQL)
            if (!trigger.Condition.Contains("SELECT", StringComparison.OrdinalIgnoreCase))
                continue;

            var triggerTable = trigger.Event?.Split('.').FirstOrDefault();
            if (string.IsNullOrWhiteSpace(triggerTable)) continue;

            var triggerEntity = spec.Entities.FirstOrDefault(e =>
                e.Table.Equals(triggerTable, StringComparison.OrdinalIgnoreCase));
            if (triggerEntity == null) continue;

            var result = _conditionNormalizer.Normalize(trigger.Condition, spec, triggerEntity);
            if (result.WasNormalized)
            {
                var oldCondition = trigger.Condition;
                trigger.Condition = result.NormalizedCondition;
                repairs.Add(new RepairAction
                {
                    Path = $"triggers[name={trigger.Name}].condition",
                    Message = $"Normalized SQL subquery in condition: '{oldCondition}' → '{result.NormalizedCondition}'"
                });
            }
        }
    }

    private static string NormalizeJsonKeys(string json)
    {
        // Normalize common AI snake_case aliases to camelCase
        json = json.Replace("\"initial_state\":", "\"initialState\":");
        json = json.Replace("\"state_field\":", "\"stateField\":");
        return json;
    }

    private static bool HasWorkflowIntent(string prompt) =>
        WorkflowIntentKeywords.Any(k => prompt.Contains(k, StringComparison.OrdinalIgnoreCase));

    private static bool HasTriggerIntent(string prompt) =>
        TriggerIntentKeywords.Any(k => prompt.Contains(k, StringComparison.OrdinalIgnoreCase));
}

public class GenerationResult
{
    public bool IsValid => Validation.IsValid;
    public BackendSpec? Spec => Validation.Spec;
    public string? Error => Validation.Error;
    public SpecValidationResult Validation { get; set; } = new();
    public List<RepairAction> Repairs { get; set; } = new();
}

public class RepairAction
{
    public string Path { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
