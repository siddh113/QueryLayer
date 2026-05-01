using Microsoft.AspNetCore.Mvc;
using Npgsql;
using QueryLayer.Api.Services.Auth;
using QueryLayer.Api.Services.Runtime;
using QueryLayer.Api.Services.Workflow;

namespace QueryLayer.Api.Controllers;

/// <summary>
/// Handles workflow state transitions: POST /api/{projectKey}/{entity}/{id}/{transition}
/// </summary>
[ApiController]
public class WorkflowController : ControllerBase
{
    private readonly ProjectRuntimeResolver _resolver;
    private readonly RbacEvaluator _rbacEvaluator;
    private readonly WorkflowEngine _workflowEngine;
    private readonly IConfiguration _configuration;

    public WorkflowController(
        ProjectRuntimeResolver resolver,
        RbacEvaluator rbacEvaluator,
        WorkflowEngine workflowEngine,
        IConfiguration configuration)
    {
        _resolver = resolver;
        _rbacEvaluator = rbacEvaluator;
        _workflowEngine = workflowEngine;
        _configuration = configuration;
    }

    [HttpPost]
    [Route("api/{projectKey}/{entity}/{id}/{transition}")]
    public async Task<IActionResult> Transition(
        string projectKey, string entity, string id, string transition)
    {
        var spec = await _resolver.ResolveSpecAsync(projectKey);
        if (spec == null)
            return NotFound(new { error = "Project not found" });

        var projectId = await _resolver.ResolveProjectIdAsync(projectKey);
        if (projectId == null)
            return NotFound(new { error = "Project not found" });

        var workflow = spec.Workflows.FirstOrDefault(w =>
            w.Entity.Equals(entity, StringComparison.OrdinalIgnoreCase));

        if (workflow == null)
            return NotFound(new { error = $"No workflow defined for entity '{entity}'" });

        // Read current record state from DB
        var entitySpec = spec.Entities.FirstOrDefault(e =>
            e.Name.Equals(entity, StringComparison.OrdinalIgnoreCase));
        if (entitySpec == null)
            return NotFound(new { error = "Entity not found in spec" });

        var currentState = await GetCurrentStateAsync(entitySpec.Table, id);

        var authContext = BuildAuthContext();
        var result = await _workflowEngine.TransitionAsync(
            spec, projectId.Value, entity, id,
            transition, currentState,
            authContext.Role, authContext.UserId);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.ErrorMessage });

        // Update state in DB
        await UpdateStateAsync(entitySpec.Table, id, result.NewState!);

        return Ok(new { id, state = result.NewState, transition });
    }

    private async Task<string?> GetCurrentStateAsync(string table, string id)
    {
        var connStr = Environment.GetEnvironmentVariable("DATABASE_URL") ??
                      _configuration.GetConnectionString("DefaultConnection");
        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            $"SELECT state FROM {table} WHERE id = @id LIMIT 1", conn);
        cmd.Parameters.AddWithValue("id", id);
        var val = await cmd.ExecuteScalarAsync();
        return val as string;
    }

    private async Task UpdateStateAsync(string table, string id, string newState)
    {
        var connStr = Environment.GetEnvironmentVariable("DATABASE_URL") ??
                      _configuration.GetConnectionString("DefaultConnection");
        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            $"UPDATE {table} SET state = @state WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("state", newState);
        cmd.Parameters.AddWithValue("id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    private AuthContext BuildAuthContext()
    {
        var userIdStr = HttpContext.Items["user_id"] as string;
        var role = HttpContext.Items["role"] as string;

        if (userIdStr == null || !Guid.TryParse(userIdStr, out var userId))
            return AuthContext.Anonymous();

        return new AuthContext
        {
            IsAuthenticated = true,
            UserId = userId,
            Role = role ?? "user",
            AuthMethod = "jwt"
        };
    }
}
