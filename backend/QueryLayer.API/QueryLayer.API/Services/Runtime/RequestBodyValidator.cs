using System.Text.Json;
using QueryLayer.Api.Models.Runtime;

namespace QueryLayer.Api.Services.Runtime;

public class RequestBodyValidator
{
    public List<string> Validate(
        EntitySpec entity,
        Dictionary<string, object?> body,
        string operation)
    {
        var errors = new List<string>();

        foreach (var field in entity.Fields)
        {
            if (field.Primary) continue;

            if (operation.Equals("create", StringComparison.OrdinalIgnoreCase)
                && field.Required
                && !body.ContainsKey(field.Name))
            {
                errors.Add($"Field '{field.Name}' is required.");
                continue;
            }

            if (!body.TryGetValue(field.Name, out var value) || value == null)
                continue;

            if (!IsValidType(value, field.Type))
                errors.Add($"Field '{field.Name}' must be of type '{field.Type}'.");
        }

        return errors;
    }

    private static bool IsValidType(object? value, string type)
    {
        if (value is JsonElement element)
        {
            return type.ToLowerInvariant() switch
            {
                "string" or "text" => element.ValueKind == JsonValueKind.String,
                "integer" => element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out _),
                "boolean" => element.ValueKind is JsonValueKind.True or JsonValueKind.False,
                "uuid" => element.ValueKind == JsonValueKind.String && Guid.TryParse(element.GetString(), out _),
                "timestamp" => element.ValueKind == JsonValueKind.String && DateTime.TryParse(element.GetString(), out _),
                _ => true
            };
        }

        return true;
    }
}
