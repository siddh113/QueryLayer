using System.Text.Json;
using QueryLayer.Api.Models.Runtime;

namespace QueryLayer.Api.Services.AI;

public class SpecValidator
{
    // Platform-managed tables that specs must not touch
    private static readonly HashSet<string> ReservedTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "users", "projects", "project_specs", "project_keys", "auth_users", "platform_users"
    };

    private static readonly HashSet<string> ValidFieldTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "string", "integer", "boolean", "uuid", "timestamp", "text"
    };

    private static readonly HashSet<string> ValidMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET", "POST", "PUT", "PATCH", "DELETE"
    };

    private static readonly HashSet<string> ValidOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "list", "read", "create", "update", "delete"
    };

    private static readonly HashSet<string> ValidAuthModes = new(StringComparer.OrdinalIgnoreCase)
    {
        "public", "authenticated", "admin"
    };

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

        var errors = new List<string>();

        if (spec.Entities == null || spec.Entities.Count == 0)
            errors.Add("Spec must have at least one entity.");

        var entityNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (spec.Entities != null)
        {
            foreach (var entity in spec.Entities)
            {
                if (string.IsNullOrWhiteSpace(entity.Name))
                {
                    errors.Add("Entity name is required.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entity.Table))
                    errors.Add($"Entity '{entity.Name}' must have a table name.");
                else if (ReservedTables.Contains(entity.Table))
                    errors.Add($"Table name '{entity.Table}' is reserved by the platform. Use a different name (e.g., 'app_users', 'members').");

                if (entity.Fields == null || entity.Fields.Count == 0)
                {
                    errors.Add($"Entity '{entity.Name}' must have at least one field.");
                    continue;
                }

                if (!entity.Fields.Any(f => f.Primary))
                    errors.Add($"Entity '{entity.Name}' must have a primary key field.");

                foreach (var field in entity.Fields)
                {
                    if (string.IsNullOrWhiteSpace(field.Name))
                        errors.Add($"Entity '{entity.Name}' has a field with no name.");
                    else if (!ValidFieldTypes.Contains(field.Type))
                        errors.Add($"Entity '{entity.Name}', field '{field.Name}' has invalid type '{field.Type}'.");
                }

                entityNames.Add(entity.Name);
            }
        }

        if (spec.Endpoints != null)
        {
            foreach (var ep in spec.Endpoints)
            {
                if (!ValidMethods.Contains(ep.Method))
                    errors.Add($"Endpoint has invalid method '{ep.Method}'.");
                if (string.IsNullOrWhiteSpace(ep.Path))
                    errors.Add("Endpoint path is required.");
                if (!ValidOperations.Contains(ep.Operation))
                    errors.Add($"Endpoint has invalid operation '{ep.Operation}'.");
                if (!string.IsNullOrWhiteSpace(ep.Entity) && entityNames.Count > 0 && !entityNames.Contains(ep.Entity))
                    errors.Add($"Endpoint references unknown entity '{ep.Entity}'.");
                if (!string.IsNullOrWhiteSpace(ep.Auth) && !ValidAuthModes.Contains(ep.Auth))
                    errors.Add($"Endpoint has invalid auth mode '{ep.Auth}'.");
            }
        }

        if (spec.Permissions != null)
        {
            foreach (var perm in spec.Permissions)
            {
                if (!string.IsNullOrWhiteSpace(perm.Entity) && entityNames.Count > 0 && !entityNames.Contains(perm.Entity))
                    errors.Add($"Permission references unknown entity '{perm.Entity}'.");
                if (perm.Operations != null)
                {
                    foreach (var op in perm.Operations)
                    {
                        if (!ValidOperations.Contains(op))
                            errors.Add($"Permission has invalid operation '{op}'.");
                    }
                }
            }
        }

        return errors.Count > 0
            ? SpecValidationResult.Failure(string.Join("; ", errors))
            : SpecValidationResult.Success(spec);
    }
}

public class SpecValidationResult
{
    public bool IsValid { get; set; }
    public string? Error { get; set; }
    public BackendSpec? Spec { get; set; }

    public static SpecValidationResult Success(BackendSpec spec) => new() { IsValid = true, Spec = spec };
    public static SpecValidationResult Failure(string error) => new() { IsValid = false, Error = error };
}
