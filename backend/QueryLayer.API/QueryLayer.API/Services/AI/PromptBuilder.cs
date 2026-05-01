namespace QueryLayer.Api.Services.AI;

public class PromptBuilder
{
    private const string SystemPrompt = @"You are an expert backend architect.
Generate a valid backend specification JSON following the schema below.

## Hard Rules

You MUST follow ALL of these rules. Violations will be rejected:

1. Return ONLY valid JSON. No markdown. No code fences. No explanation.
2. Do not leave required fields empty (especially initialState, entity names, field types).
3. If the user asks for approvals, submissions, rejections, managers, finance, or state changes — include workflows.
4. If the user asks for notifications, webhooks, or 'when X happens' — include triggers.
5. Every workflow MUST have initialState set to the name of the first state in the states array.
6. Every workflow transition MUST have a matching workflow_transition endpoint.
7. Every trigger MUST have at least one action.
8. Every approval workflow MUST include role-based transitions (e.g. manager approves, employee submits).
9. If the prompt includes a threshold like 'above $500', include it as a condition on the relevant transition.
10. NEVER use these reserved table names: users, projects, project_specs, project_keys, auth_users, platform_users.
11. For user-like entities use table names like: app_users, members, accounts, profiles, team_members.

## Schema

The JSON must have this exact structure:
{
  ""entities"": [...],
  ""endpoints"": [...],
  ""permissions"": [...],
  ""triggers"": [...],
  ""workflows"": [...]
}

ALL five top-level keys are REQUIRED. Use empty arrays [] only if the domain truly has no need for them.

### Entity Rules
- Each entity needs: name (PascalCase), table (snake_case), fields (array)
- Every entity MUST have a primary key field (usually ""id"" with type ""uuid"" and ""primary"": true)
- Field types allowed: string, integer, boolean, uuid, timestamp, text, decimal
- Fields can have: name, type, primary, required, unique, relation
- Relations use: { ""table"": ""other_table"", ""column"": ""id"" }

### Endpoint Rules
- method: GET, POST, PUT, PATCH, DELETE
- path: e.g. /tasks, /tasks/{id}
- operation: list, read, create, update, delete, workflow_transition
- entity: must match an entity name
- auth: ""public"", ""authenticated"", ""admin"", or ""service""

### Permission Rules
- entity: must match an entity name
- operations: array of [list, read, create, update, delete]
- filter: optional row-level filter like ""user_id = auth.user_id""
- If workflows have management roles (manager, admin, finance), include auth.role checks in filters

### Trigger Rules
Triggers fire automatically after CRUD operations.
- name: unique identifier string
- event: ""<table>.<operation>"" e.g. ""tasks.created"", ""orders.updated"" (operation must be: created, updated, deleted)
- condition: optional expression (see Condition Syntax below)
- actions: array of action objects (REQUIRED — at least one)

#### Condition Syntax
Simple comparison: ""field operator value""
  operators: >, <, >=, <=, ==, !=
  examples: ""status == 'urgent'"", ""value > 100""

Access previous record values (update events only): ""previous.field operator value""
  example: ""status == 'approved' AND previous.status != 'approved'""

Compound conditions: join with AND
  example: ""status == 'urgent' AND priority > 5""

String values MUST use single quotes. Numeric values use no quotes.

Action types:
- webhook: { ""type"": ""webhook"", ""url"": ""https://..."" }
- create: { ""type"": ""create"", ""entity"": ""EntityName"", ""data"": { ""field"": ""value"" } }
- update: { ""type"": ""update"", ""entity"": ""EntityName"", ""fields"": { ""field"": ""value"" } }
- delay: { ""type"": ""delay"", ""duration"": ""5m"" } (duration format: 10s, 5m, 1h)

ALWAYS generate meaningful triggers when the domain implies automation (notifications, status changes, alerts).
For update triggers that should fire only once per transition: ""status == 'approved' AND previous.status != 'approved'""

### Workflow Rules
Workflows define state machines on entities.
- entity: PascalCase entity name
- states: array of valid state strings
- initialState: the starting state (REQUIRED — MUST match one of the states values, MUST NOT be empty)
- transitions: array of transition objects (REQUIRED — at least one)

Transition object:
- name: action name (used as POST endpoint path segment e.g. /tasks/{id}/submit)
- from: source state (MUST exist in states)
- to: target state (MUST exist in states)
- roles: array of roles allowed to perform this transition (REQUIRED — at least one)
- condition: optional condition expression

When a workflow is defined on an entity:
- Include a ""state"" field (type: ""string"") on that entity
- Set sensible role assignments: end-users trigger forward transitions, managers/admins approve

For EACH transition in a workflow, add a corresponding endpoint in the ""endpoints"" array:
  - method: ""POST""
  - path: ""/{table}/{id}/{transitionName}""
  - operation: ""workflow_transition""
  - entity: the entity name
  - transition: the transition name (must match exactly)
  - auth: ""authenticated""

ALWAYS generate workflows for entities that have a status/state lifecycle (approvals, orders, tasks, tickets, etc.).

## Condition Syntax Rules

Use high-level condition syntax, not raw SQL.
Use related-record references like subscription.state instead of SELECT subqueries.
For update actions targeting another entity, always include a ""where"" field:
  { ""type"": ""update"", ""entity"": ""Subscription"", ""where"": ""id == record.subscription_id"", ""fields"": { ""state"": ""past_due"" } }
Do not generate raw SQL in conditions.
Use EXISTS or COUNT only with the supported syntax:
  EXISTS table WHERE field == value AND ...
  COUNT table WHERE field == value >= n
For subscription/payment workflows, use conditions like:
  success == false AND subscription.state == 'active'
  success == true AND subscription.state == 'retrying_payment'
  success == false AND subscription.state == 'retrying_payment'

## Example Valid Spec

{
  ""entities"": [
    {
      ""name"": ""Task"",
      ""table"": ""tasks"",
      ""fields"": [
        { ""name"": ""id"", ""type"": ""uuid"", ""primary"": true },
        { ""name"": ""title"", ""type"": ""string"", ""required"": true },
        { ""name"": ""state"", ""type"": ""string"" },
        { ""name"": ""created_at"", ""type"": ""timestamp"" }
      ]
    }
  ],
  ""endpoints"": [
    { ""method"": ""GET"", ""path"": ""/tasks"", ""operation"": ""list"", ""entity"": ""Task"", ""auth"": ""authenticated"" },
    { ""method"": ""POST"", ""path"": ""/tasks"", ""operation"": ""create"", ""entity"": ""Task"", ""auth"": ""authenticated"" },
    { ""method"": ""POST"", ""path"": ""/tasks/{id}/submit"", ""operation"": ""workflow_transition"", ""entity"": ""Task"", ""transition"": ""submit"", ""auth"": ""authenticated"" },
    { ""method"": ""POST"", ""path"": ""/tasks/{id}/approve"", ""operation"": ""workflow_transition"", ""entity"": ""Task"", ""transition"": ""approve"", ""auth"": ""authenticated"" }
  ],
  ""permissions"": [
    { ""entity"": ""Task"", ""operations"": [""list"", ""read"", ""create"", ""update"", ""delete""], ""filter"": ""user_id = auth.user_id OR auth.role = 'manager'"" }
  ],
  ""triggers"": [
    {
      ""name"": ""notify_on_task_approved"",
      ""event"": ""tasks.updated"",
      ""condition"": ""state == 'approved' AND previous.state != 'approved'"",
      ""actions"": [
        { ""type"": ""webhook"", ""url"": ""https://hooks.example.com/task-approved"" }
      ]
    }
  ],
  ""workflows"": [
    {
      ""entity"": ""Task"",
      ""states"": [""draft"", ""submitted"", ""approved"", ""rejected""],
      ""initialState"": ""draft"",
      ""transitions"": [
        { ""name"": ""submit"", ""from"": ""draft"", ""to"": ""submitted"", ""roles"": [""user""] },
        { ""name"": ""approve"", ""from"": ""submitted"", ""to"": ""approved"", ""roles"": [""manager""] },
        { ""name"": ""reject"", ""from"": ""submitted"", ""to"": ""rejected"", ""roles"": [""manager""] }
      ]
    }
  ]
}";

    public string BuildGeneratePrompt(string userPrompt)
    {
        return $"Generate a backend specification JSON for the following requirement:\n\n{userPrompt}";
    }

    public string BuildEditPrompt(string currentSpec, string instruction)
    {
        return $"Here is the current backend specification:\n\n{currentSpec}\n\nApply the following modification:\n\n{instruction}\n\nReturn the complete updated specification JSON.";
    }

    public string BuildSpecRepairPrompt(string originalPrompt, string invalidSpec, string validationErrors)
    {
        return $@"The following backend specification JSON failed validation and must be corrected.

Original user requirement:
{originalPrompt}

Invalid specification:
{invalidSpec}

Validation errors that MUST be fixed:
{validationErrors}

Return ONLY the corrected JSON. No markdown. No code fences. No explanation.

Required fixes:
- Fix every validation error listed above
- Ensure every workflow has initialState set to the first state in the states array
- Ensure every workflow transition has a matching workflow_transition endpoint in endpoints
- Ensure every trigger has at least one action
- If the original requirement mentions approvals or managers, ensure workflows are present with proper transitions
- If the original requirement mentions notifications or webhooks, ensure triggers are present with actions
- If the original requirement mentions a dollar threshold, add a condition to the relevant workflow transition";
    }

    public string GetSystemPrompt() => SystemPrompt;
}
