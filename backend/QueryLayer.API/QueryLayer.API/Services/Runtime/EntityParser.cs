using System.Text.Json;
using QueryLayer.Api.Models.Runtime;

namespace QueryLayer.Api.Services.Runtime;

public class EntityParser
{
    public BackendSpec Parse(string specJson)
    {
        var spec = JsonSerializer.Deserialize<BackendSpec>(specJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (spec == null)
            throw new InvalidOperationException("Failed to parse backend spec JSON.");

        Validate(spec);
        return spec;
    }

    public List<EntitySpec> ParseEntities(string specJson)
    {
        var spec = Parse(specJson);
        return spec.Entities;
    }

    private static readonly HashSet<string> ReservedTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "users", "projects", "project_specs", "project_keys", "auth_users", "platform_users"
    };

    private static void Validate(BackendSpec spec)
    {
        foreach (var entity in spec.Entities)
        {
            if (string.IsNullOrWhiteSpace(entity.Name))
                throw new InvalidOperationException("Entity name is required.");
            if (string.IsNullOrWhiteSpace(entity.Table))
                throw new InvalidOperationException($"Table name is required for entity '{entity.Name}'.");
            if (ReservedTables.Contains(entity.Table))
                throw new InvalidOperationException($"Table name '{entity.Table}' is reserved by the platform. Use a different name (e.g., 'app_users', 'members').");
            if (entity.Fields.Count == 0)
                throw new InvalidOperationException($"Entity '{entity.Name}' must have at least one field.");
            if (!entity.Fields.Any(f => f.Primary))
                throw new InvalidOperationException($"Entity '{entity.Name}' must have a primary key field.");
        }
    }
}
