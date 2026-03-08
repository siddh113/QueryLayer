using Microsoft.AspNetCore.Mvc;
using QueryLayer.Api.Services.Runtime;

namespace QueryLayer.Api.Controllers;

[ApiController]
public class RuntimeController : ControllerBase
{
    private readonly ProjectRuntimeResolver _resolver;
    private readonly EndpointMatcher _matcher;
    private readonly RequestBodyValidator _validator;
    private readonly RuntimeExecutor _executor;

    public RuntimeController(
        ProjectRuntimeResolver resolver,
        EndpointMatcher matcher,
        RequestBodyValidator validator,
        RuntimeExecutor executor)
    {
        _resolver = resolver;
        _matcher = matcher;
        _validator = validator;
        _executor = executor;
    }

    [HttpGet]
    [Route("api/{projectKey}/{**path}")]
    public Task<IActionResult> HandleGet(string projectKey, string path) =>
        Handle(projectKey, path, null);

    [HttpPost]
    [Route("api/{projectKey}/{**path}")]
    public Task<IActionResult> HandlePost(string projectKey, string path,
        [FromBody] Dictionary<string, object?> body) =>
        Handle(projectKey, path, body);

    [HttpPut]
    [Route("api/{projectKey}/{**path}")]
    public Task<IActionResult> HandlePut(string projectKey, string path,
        [FromBody] Dictionary<string, object?> body) =>
        Handle(projectKey, path, body);

    [HttpPatch]
    [Route("api/{projectKey}/{**path}")]
    public Task<IActionResult> HandlePatch(string projectKey, string path,
        [FromBody] Dictionary<string, object?> body) =>
        Handle(projectKey, path, body);

    [HttpDelete]
    [Route("api/{projectKey}/{**path}")]
    public Task<IActionResult> HandleDelete(string projectKey, string path) =>
        Handle(projectKey, path, null);

    private async Task<IActionResult> Handle(
        string projectKey, string path, Dictionary<string, object?>? body)
    {
        var spec = await _resolver.ResolveSpecAsync(projectKey);
        if (spec == null)
            return NotFound(new { error = "Project not found" });

        var method = HttpContext.Request.Method;
        var normalizedPath = "/" + Uri.UnescapeDataString(path ?? "").TrimStart('/');
        var (endpoint, pathParams) = _matcher.Match(spec, method, normalizedPath);
        if (endpoint == null)
            return NotFound(new { error = "Endpoint not found" });

        var entity = spec.Entities.FirstOrDefault(e =>
            e.Name.Equals(endpoint.Entity, StringComparison.OrdinalIgnoreCase));
        if (entity == null)
            return NotFound(new { error = "Entity not found" });

        if (method is "POST" or "PUT" or "PATCH")
        {
            body ??= new();
            var errors = _validator.Validate(entity, body, endpoint.Operation);
            if (errors.Count > 0)
                return BadRequest(new { error = "Invalid request", details = errors });
        }

        var queryParams = HttpContext.Request.Query
            .ToDictionary(q => q.Key, q => q.Value.ToString());

        var result = await _executor.ExecuteAsync(endpoint, entity, pathParams, body, queryParams);
        return Ok(result);
    }
}
