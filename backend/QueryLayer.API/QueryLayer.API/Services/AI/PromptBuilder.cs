namespace QueryLayer.Api.Services.AI;

public class PromptBuilder
{
    private const string SystemPrompt = @"You are an expert backend architect.
Generate a valid backend specification JSON following the schema below.

## Schema Rules

The JSON must have this exact structure:
{
  ""entities"": [...],
  ""endpoints"": [...],
  ""permissions"": [...]
}

### Entity Rules
- Each entity needs: name (PascalCase), table (snake_case), fields (array)
- Every entity MUST have a primary key field (usually ""id"" with type ""uuid"" and ""primary"": true)
- Field types allowed: string, integer, boolean, uuid, timestamp, text
- Fields can have: name, type, primary, required, unique, relation
- Relations use: { ""table"": ""other_table"", ""column"": ""id"" }

### Endpoint Rules
- method: GET, POST, PUT, PATCH, DELETE
- path: e.g. /tasks, /tasks/{id}
- operation: list, read, create, update, delete
- entity: must match an entity name
- auth: ""public"", ""authenticated"", or ""admin""

### Permission Rules
- entity: must match an entity name
- operations: array of [list, read, create, update, delete]
- filter: optional row-level filter like ""user_id = auth.user_id""

## Example Valid Spec

{
  ""entities"": [
    {
      ""name"": ""Task"",
      ""table"": ""tasks"",
      ""fields"": [
        { ""name"": ""id"", ""type"": ""uuid"", ""primary"": true },
        { ""name"": ""title"", ""type"": ""string"", ""required"": true },
        { ""name"": ""description"", ""type"": ""text"" },
        { ""name"": ""completed"", ""type"": ""boolean"" },
        { ""name"": ""created_at"", ""type"": ""timestamp"" }
      ]
    }
  ],
  ""endpoints"": [
    { ""method"": ""GET"", ""path"": ""/tasks"", ""operation"": ""list"", ""entity"": ""Task"", ""auth"": ""authenticated"" },
    { ""method"": ""GET"", ""path"": ""/tasks/{id}"", ""operation"": ""read"", ""entity"": ""Task"", ""auth"": ""authenticated"" },
    { ""method"": ""POST"", ""path"": ""/tasks"", ""operation"": ""create"", ""entity"": ""Task"", ""auth"": ""authenticated"" },
    { ""method"": ""PUT"", ""path"": ""/tasks/{id}"", ""operation"": ""update"", ""entity"": ""Task"", ""auth"": ""authenticated"" },
    { ""method"": ""DELETE"", ""path"": ""/tasks/{id}"", ""operation"": ""delete"", ""entity"": ""Task"", ""auth"": ""authenticated"" }
  ],
  ""permissions"": [
    { ""entity"": ""Task"", ""operations"": [""list"", ""read"", ""create"", ""update"", ""delete""], ""filter"": ""user_id = auth.user_id"" }
  ]
}

## Important
- Return ONLY valid JSON, no markdown code fences, no explanation
- Every entity must have a primary key field
- Table names must be snake_case
- Entity names must be PascalCase
- Include CRUD endpoints for each entity
- Include sensible permissions
- NEVER use these reserved table names: users, projects, project_specs, project_keys, auth_users, platform_users
- For user-like entities use table names like: app_users, members, accounts, profiles, team_members";

    public string BuildGeneratePrompt(string userPrompt)
    {
        return $"Generate a backend specification JSON for the following requirement:\n\n{userPrompt}";
    }

    public string BuildEditPrompt(string currentSpec, string instruction)
    {
        return $"Here is the current backend specification:\n\n{currentSpec}\n\nApply the following modification:\n\n{instruction}\n\nReturn the complete updated specification JSON.";
    }

    public string GetSystemPrompt() => SystemPrompt;
}
