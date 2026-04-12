using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using QueryLayer.Api.Data;
using QueryLayer.Api.Services.Auth;
using QueryLayer.Api.Services.Runtime;

namespace QueryLayer.Api.Controllers;

[ApiController]
[Route("platform")]
public class PlatformController : ControllerBase
{
    private readonly PlatformAuthService _platformAuth;
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;

    public PlatformController(
        PlatformAuthService platformAuth,
        AppDbContext db,
        IConfiguration configuration)
    {
        _platformAuth = platformAuth;
        _db = db;
        _configuration = configuration;
    }

    // --- Auth ---

    [HttpPost("signup")]
    public async Task<IActionResult> Signup([FromBody] PlatformAuthRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Email and password are required." });

        var result = await _platformAuth.SignupAsync(request.Email, request.Password);

        if (!result.Success)
            return Conflict(new { error = result.Error });

        return Ok(new { token = result.Token, userId = result.UserId, role = result.Role });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] PlatformAuthRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Email and password are required." });

        var result = await _platformAuth.LoginAsync(request.Email, request.Password);

        if (!result.Success)
            return Unauthorized(new { error = result.Error });

        return Ok(new { token = result.Token, userId = result.UserId, role = result.Role });
    }

    // --- Projects (raw SQL to avoid EF Core column mapping issues) ---

    [HttpGet("projects")]
    public async Task<IActionResult> GetProjects()
    {
        var userId = GetPlatformUserId();
        if (userId == null)
            return Unauthorized(new { error = "Authentication required." });

        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, name, owner_user_id, created_at FROM projects WHERE owner_user_id = @userId ORDER BY created_at DESC",
            conn);
        cmd.Parameters.AddWithValue("userId", userId.Value);

        var projects = new List<object>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            projects.Add(new
            {
                id = reader.GetGuid(0),
                name = reader.GetString(1),
                ownerUserId = reader.IsDBNull(2) ? (Guid?)null : reader.GetGuid(2),
                createdAt = reader.GetDateTime(3)
            });
        }

        return Ok(projects);
    }

    [HttpPost("projects")]
    public async Task<IActionResult> CreateProject([FromBody] CreateProjectRequest request)
    {
        var userId = GetPlatformUserId();
        if (userId == null)
            return Unauthorized(new { error = "Authentication required." });

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Project name is required." });

        var projectId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;

        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(@"
            INSERT INTO projects (id, name, owner_user_id, created_at)
            VALUES (@id, @name, @ownerUserId, @createdAt)", conn);

        cmd.Parameters.AddWithValue("id", projectId);
        cmd.Parameters.AddWithValue("name", request.Name);
        cmd.Parameters.AddWithValue("ownerUserId", userId.Value);
        cmd.Parameters.AddWithValue("createdAt", createdAt);

        await cmd.ExecuteNonQueryAsync();

        return Ok(new
        {
            id = projectId,
            name = request.Name,
            ownerUserId = userId.Value,
            createdAt
        });
    }

    [HttpGet("projects/{id}")]
    public async Task<IActionResult> GetProject(Guid id)
    {
        var userId = GetPlatformUserId();
        if (userId == null)
            return Unauthorized(new { error = "Authentication required." });

        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, name, owner_user_id, created_at FROM projects WHERE id = @id AND owner_user_id = @userId",
            conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("userId", userId.Value);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return NotFound(new { error = "Project not found." });

        return Ok(new
        {
            id = reader.GetGuid(0),
            name = reader.GetString(1),
            ownerUserId = reader.IsDBNull(2) ? (Guid?)null : reader.GetGuid(2),
            createdAt = reader.GetDateTime(3)
        });
    }

    [HttpGet("projects/{id}/spec")]
    public async Task<IActionResult> GetSpec(Guid id)
    {
        var userId = GetPlatformUserId();
        if (userId == null)
            return Unauthorized(new { error = "Authentication required." });

        // Verify ownership
        await using var conn = await OpenConnectionAsync();
        await using (var checkCmd = new NpgsqlCommand(
            "SELECT COUNT(*) FROM projects WHERE id = @id AND owner_user_id = @userId", conn))
        {
            checkCmd.Parameters.AddWithValue("id", id);
            checkCmd.Parameters.AddWithValue("userId", userId.Value);
            var count = (long)(await checkCmd.ExecuteScalarAsync())!;
            if (count == 0)
                return NotFound(new { error = "Project not found." });
        }

        // Get latest spec
        await using var specCmd = new NpgsqlCommand(@"
            SELECT spec_json, version FROM project_specs
            WHERE project_id = @projectId
            ORDER BY version DESC LIMIT 1", conn);
        specCmd.Parameters.AddWithValue("projectId", id);

        await using var reader = await specCmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return Ok(new { specJson = "{\"entities\":[],\"endpoints\":[],\"permissions\":[]}", version = 0 });

        return Ok(new { specJson = reader.GetString(0), version = reader.GetInt32(1) });
    }

    private Guid? GetPlatformUserId()
    {
        var userIdStr = HttpContext.Items["user_id"] as string;
        if (userIdStr != null && Guid.TryParse(userIdStr, out var userId))
            return userId;
        return null;
    }

    private async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        var connectionString =
            Environment.GetEnvironmentVariable("DATABASE_URL") ??
            _configuration.GetConnectionString("DefaultConnection");

        var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        return conn;
    }
}

public class PlatformAuthRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class CreateProjectRequest
{
    public string Name { get; set; } = string.Empty;
}
