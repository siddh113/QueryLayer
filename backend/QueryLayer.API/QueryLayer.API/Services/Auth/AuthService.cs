using Npgsql;

namespace QueryLayer.Api.Services.Auth;

public class AuthService
{
    private readonly IConfiguration _configuration;
    private readonly PasswordHasher _passwordHasher;
    private readonly JwtService _jwtService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IConfiguration configuration,
        PasswordHasher passwordHasher,
        JwtService jwtService,
        ILogger<AuthService> logger)
    {
        _configuration = configuration;
        _passwordHasher = passwordHasher;
        _jwtService = jwtService;
        _logger = logger;
    }

    public async Task<AuthResult> SignupAsync(Guid projectId, string email, string password)
    {
        await using var conn = await OpenConnectionAsync();

        // Check if user already exists
        await using (var checkCmd = new NpgsqlCommand(
            "SELECT COUNT(*) FROM \"auth_users\" WHERE \"email\" = @email AND \"project_id\" = @projectId", conn))
        {
            checkCmd.Parameters.AddWithValue("email", email);
            checkCmd.Parameters.AddWithValue("projectId", projectId);
            var count = (long)(await checkCmd.ExecuteScalarAsync())!;
            if (count > 0)
            {
                _logger.LogWarning("Signup failed: email {Email} already exists for project {ProjectId}.", email, projectId);
                return AuthResult.Fail("Email already registered.");
            }
        }

        var userId = Guid.NewGuid();
        var passwordHash = _passwordHasher.Hash(password);

        await using (var insertCmd = new NpgsqlCommand(@"
            INSERT INTO ""auth_users"" (""id"", ""project_id"", ""email"", ""password_hash"", ""role"", ""created_at"")
            VALUES (@id, @projectId, @email, @passwordHash, @role, @createdAt)", conn))
        {
            insertCmd.Parameters.AddWithValue("id", userId);
            insertCmd.Parameters.AddWithValue("projectId", projectId);
            insertCmd.Parameters.AddWithValue("email", email);
            insertCmd.Parameters.AddWithValue("passwordHash", passwordHash);
            insertCmd.Parameters.AddWithValue("role", "user");
            insertCmd.Parameters.AddWithValue("createdAt", DateTime.UtcNow);
            await insertCmd.ExecuteNonQueryAsync();
        }

        var token = _jwtService.GenerateToken(userId, projectId, "user");
        _logger.LogInformation("User {UserId} signed up for project {ProjectId}.", userId, projectId);

        return AuthResult.Ok(token, userId, "user");
    }

    public async Task<AuthResult> LoginAsync(Guid projectId, string email, string password)
    {
        await using var conn = await OpenConnectionAsync();

        await using var cmd = new NpgsqlCommand(@"
            SELECT ""id"", ""password_hash"", ""role""
            FROM ""auth_users""
            WHERE ""email"" = @email AND ""project_id"" = @projectId", conn);

        cmd.Parameters.AddWithValue("email", email);
        cmd.Parameters.AddWithValue("projectId", projectId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            _logger.LogWarning("Login failed: email {Email} not found for project {ProjectId}.", email, projectId);
            return AuthResult.Fail("Invalid email or password.");
        }

        var userId = reader.GetGuid(0);
        var hash = reader.GetString(1);
        var role = reader.GetString(2);

        if (!_passwordHasher.Verify(password, hash))
        {
            _logger.LogWarning("Login failed: invalid password for user {UserId}.", userId);
            return AuthResult.Fail("Invalid email or password.");
        }

        var token = _jwtService.GenerateToken(userId, projectId, role);
        _logger.LogInformation("User {UserId} logged in for project {ProjectId}.", userId, projectId);

        return AuthResult.Ok(token, userId, role);
    }

    public async Task EnsureAuthTableAsync()
    {
        await using var conn = await OpenConnectionAsync();

        var sql = @"
            CREATE TABLE IF NOT EXISTS ""auth_users"" (
                ""id"" uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                ""project_id"" uuid NOT NULL,
                ""email"" varchar(255) NOT NULL,
                ""password_hash"" varchar(255) NOT NULL,
                ""role"" varchar(50) NOT NULL DEFAULT 'user',
                ""created_at"" timestamptz DEFAULT now(),
                UNIQUE(""project_id"", ""email"")
            );
            CREATE INDEX IF NOT EXISTS ""idx_auth_users_project_email"" ON ""auth_users"" (""project_id"", ""email"");";

        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
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

public class AuthResult
{
    public bool Success { get; set; }
    public string? Token { get; set; }
    public Guid? UserId { get; set; }
    public string? Role { get; set; }
    public string? Error { get; set; }

    public static AuthResult Ok(string token, Guid userId, string role) =>
        new() { Success = true, Token = token, UserId = userId, Role = role };

    public static AuthResult Fail(string error) =>
        new() { Success = false, Error = error };
}
