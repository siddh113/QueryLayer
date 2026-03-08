using QueryLayer.Api.Models.Runtime;

namespace QueryLayer.Api.Services.Runtime;

public class EndpointMatcher
{
    public (EndpointSpec? Endpoint, Dictionary<string, string> PathParams) Match(
        BackendSpec spec, string method, string path)
    {
        foreach (var endpoint in spec.Endpoints)
        {
            if (!endpoint.Method.Equals(method, StringComparison.OrdinalIgnoreCase))
                continue;

            var pathParams = TryMatchPath(endpoint.Path, path);
            if (pathParams != null)
                return (endpoint, pathParams);
        }

        return (null, new());
    }

    private static Dictionary<string, string>? TryMatchPath(string template, string path)
    {
        var templateParts = template.Trim('/').Split('/');
        var pathParts = path.Trim('/').Split('/');

        if (templateParts.Length != pathParts.Length)
            return null;

        var pathParams = new Dictionary<string, string>();
        for (int i = 0; i < templateParts.Length; i++)
        {
            var t = templateParts[i];
            var p = pathParts[i];

            if (t.StartsWith('{') && t.EndsWith('}'))
                pathParams[t[1..^1]] = p;
            else if (!t.Equals(p, StringComparison.OrdinalIgnoreCase))
                return null;
        }

        return pathParams;
    }
}
