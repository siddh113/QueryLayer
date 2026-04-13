using QueryLayer.Api.Models.Runtime;

namespace QueryLayer.Api.Services.DX;

public class OpenApiGeneratorService
{
    public object Generate(BackendSpec spec, string projectId, string baseUrl)
    {
        var schemas = new Dictionary<string, object>();
        foreach (var entity in spec.Entities)
            schemas[entity.Name] = BuildEntitySchema(entity);

        var paths = new Dictionary<string, object>();
        foreach (var endpoint in spec.Endpoints)
        {
            var openApiPath = ToOpenApiPath(endpoint.Path);
            if (!paths.ContainsKey(openApiPath))
                paths[openApiPath] = new Dictionary<string, object>();

            var pathItem = (Dictionary<string, object>)paths[openApiPath];
            pathItem[endpoint.Method.ToLower()] = BuildOperation(endpoint, spec);
        }

        var securitySchemes = new Dictionary<string, object>
        {
            ["BearerAuth"] = new
            {
                type = "http",
                scheme = "bearer",
                bearerFormat = "JWT"
            }
        };

        return new
        {
            openapi = "3.0.0",
            info = new
            {
                title = "QueryLayer Project API",
                version = "1.0.0",
                description = $"Auto-generated API documentation for project {projectId}"
            },
            servers = new[] { new { url = $"{baseUrl}/api/{projectId}" } },
            paths,
            components = new { schemas, securitySchemes }
        };
    }

    private static Dictionary<string, object> BuildEntitySchema(EntitySpec entity)
    {
        var properties = new Dictionary<string, object>();
        var required = new List<string>();

        foreach (var field in entity.Fields)
        {
            properties[field.Name] = MapFieldType(field.Type);
            if (field.Required || field.Primary)
                required.Add(field.Name);
        }

        return new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required
        };
    }

    private static object MapFieldType(string type) => type.ToLowerInvariant() switch
    {
        "integer" => new { type = "integer" },
        "boolean" => new { type = "boolean" },
        "uuid" => new { type = "string", format = "uuid" },
        "timestamp" => new { type = "string", format = "date-time" },
        _ => new { type = "string" }
    };

    private static object BuildOperation(EndpointSpec endpoint, BackendSpec spec)
    {
        var entity = spec.Entities.FirstOrDefault(e =>
            e.Name.Equals(endpoint.Entity, StringComparison.OrdinalIgnoreCase));

        var pathParams = ExtractPathParams(endpoint.Path);
        var parameters = pathParams.Select(p => (object)new
        {
            name = p,
            @in = "path",
            required = true,
            schema = new { type = "string", format = "uuid" }
        }).ToList();

        var operation = new Dictionary<string, object>
        {
            ["summary"] = $"{endpoint.Operation} {endpoint.Entity}",
            ["tags"] = new[] { endpoint.Entity },
            ["parameters"] = parameters,
            ["responses"] = BuildResponses(endpoint, entity)
        };

        if (endpoint.Method is "POST" or "PUT" or "PATCH" && entity != null)
        {
            operation["requestBody"] = new
            {
                required = true,
                content = new Dictionary<string, object>
                {
                    ["application/json"] = new
                    {
                        schema = new { @ref = $"#/components/schemas/{entity.Name}" }
                    }
                }
            };
        }

        if (!string.IsNullOrEmpty(endpoint.Auth) &&
            !endpoint.Auth.Equals("public", StringComparison.OrdinalIgnoreCase))
        {
            operation["security"] = new[] { new Dictionary<string, object> { ["BearerAuth"] = new List<string>() } };
        }

        return operation;
    }

    private static object BuildResponses(EndpointSpec endpoint, EntitySpec? entity)
    {
        var schemaRef = entity != null
            ? (object)new { @ref = $"#/components/schemas/{entity.Name}" }
            : new { type = "object" };

        var successSchema = endpoint.Operation.ToLowerInvariant() == "list"
            ? (object)new { type = "array", items = schemaRef }
            : schemaRef;

        return new Dictionary<string, object>
        {
            ["200"] = new
            {
                description = "Success",
                content = new Dictionary<string, object>
                {
                    ["application/json"] = new { schema = successSchema }
                }
            },
            ["400"] = new { description = "Bad Request" },
            ["401"] = new { description = "Unauthorized" },
            ["404"] = new { description = "Not Found" },
            ["500"] = new { description = "Internal Server Error" }
        };
    }

    private static List<string> ExtractPathParams(string path)
    {
        var matches = System.Text.RegularExpressions.Regex.Matches(path, @"\{(\w+)\}");
        return matches.Select(m => m.Groups[1].Value).ToList();
    }

    // Convert {id} → {id} (already OpenAPI compatible)
    private static string ToOpenApiPath(string path) => path;
}
