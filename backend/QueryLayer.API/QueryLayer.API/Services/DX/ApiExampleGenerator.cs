using System.Text.Json;
using QueryLayer.Api.Models.Runtime;

namespace QueryLayer.Api.Services.DX;

public class ApiExampleGenerator
{
    public List<EndpointExample> GenerateAll(BackendSpec spec, string projectId, string baseUrl)
    {
        var examples = new List<EndpointExample>();
        foreach (var endpoint in spec.Endpoints)
        {
            var entity = spec.Entities.FirstOrDefault(e =>
                e.Name.Equals(endpoint.Entity, StringComparison.OrdinalIgnoreCase));
            examples.Add(Generate(endpoint, entity, projectId, baseUrl));
        }
        return examples;
    }

    private static EndpointExample Generate(
        EndpointSpec endpoint, EntitySpec? entity, string projectId, string baseUrl)
    {
        var apiBase = $"{baseUrl}/api/{projectId}";
        var samplePath = FillPathParams(endpoint.Path);
        var url = $"{apiBase}{samplePath}";

        var sampleBody = BuildSampleBody(endpoint, entity);
        var bodyJson = sampleBody != null
            ? JsonSerializer.Serialize(sampleBody, new JsonSerializerOptions { WriteIndented = true })
            : null;

        var authHeader = IsProtected(endpoint.Auth)
            ? " \\\n  -H \"Authorization: Bearer YOUR_JWT_TOKEN\""
            : "";

        var bodyFlag = bodyJson != null
            ? $" \\\n  -H \"Content-Type: application/json\" \\\n  -d '{bodyJson.Replace("\n", "\n  ")}'"
            : "";

        var curl = $"curl -X {endpoint.Method} \"{url}\"{authHeader}{bodyFlag}";

        var fetchBody = bodyJson != null
            ? $",\n  body: JSON.stringify({bodyJson})"
            : "";
        var fetchHeaders = BuildFetchHeaders(endpoint, sampleBody != null);
        var fetch = $@"const res = await fetch(""{url}"", {{
  method: ""{endpoint.Method}""{fetchHeaders}{fetchBody}
}});
const data = await res.json();";

        return new EndpointExample
        {
            Method = endpoint.Method,
            Path = endpoint.Path,
            Entity = endpoint.Entity,
            Auth = endpoint.Auth,
            SampleBody = bodyJson,
            Curl = curl,
            Fetch = fetch,
            Url = url
        };
    }

    private static string FillPathParams(string path)
    {
        return System.Text.RegularExpressions.Regex.Replace(
            path, @"\{(\w+)\}", "00000000-0000-0000-0000-000000000001");
    }

    private static Dictionary<string, object>? BuildSampleBody(EndpointSpec endpoint, EntitySpec? entity)
    {
        if (endpoint.Method is not ("POST" or "PUT" or "PATCH")) return null;
        if (entity == null) return null;

        var body = new Dictionary<string, object>();
        foreach (var field in entity.Fields)
        {
            if (field.Primary) continue;
            body[field.Name] = SampleValue(field);
        }
        return body;
    }

    private static object SampleValue(FieldSpec field) => field.Type.ToLowerInvariant() switch
    {
        "integer" => 1,
        "boolean" => false,
        "uuid" => "00000000-0000-0000-0000-000000000001",
        "timestamp" => "2026-01-01T00:00:00Z",
        _ => $"sample_{field.Name}"
    };

    private static string BuildFetchHeaders(EndpointSpec endpoint, bool hasBody)
    {
        var headers = new List<string>();
        if (hasBody) headers.Add("\"Content-Type\": \"application/json\"");
        if (IsProtected(endpoint.Auth)) headers.Add("\"Authorization\": \"Bearer YOUR_JWT_TOKEN\"");

        if (headers.Count == 0) return "";
        return $",\n  headers: {{\n    {string.Join(",\n    ", headers)}\n  }}";
    }

    private static bool IsProtected(string? auth) =>
        !string.IsNullOrEmpty(auth) && !auth.Equals("public", StringComparison.OrdinalIgnoreCase);
}

public class EndpointExample
{
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Entity { get; set; } = string.Empty;
    public string? Auth { get; set; }
    public string? SampleBody { get; set; }
    public string Curl { get; set; } = string.Empty;
    public string Fetch { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}
