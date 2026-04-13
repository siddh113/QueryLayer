using Npgsql;

namespace QueryLayer.Api.Services.Auth;

public class PlatformAuthService
{
    private readonly IConfiguration _configuration;
    private readonly PasswordHasher _passwordHasher;
    private readonly JwtService _jwtService;
    private readonly ILogger<PlatformAuthService> _logger;

    public PlatformAuthService(
        IConfiguration configuration,
        PasswordHasher passwordHasher,
        JwtService jwtService,
        ILogger<PlatformAuthService> logger)
    {
        _configuration = configuration;
        _passwordHasher = passwordHasher;
        _jwtService = jwtService;
        _logger = logger;
    }

    public async Task<AuthResult> SignupAsync(string email, string password)
    {
        await using var conn = await OpenConnectionAsync();

        await using (var checkCmd = new NpgsqlCommand(
            "SELECT COUNT(*) FROM \"platform_users\" WHERE \"email\" = @email", conn))
        {
            checkCmd.Parameters.AddWithValue("email", email);
            var count = (long)(await checkCmd.ExecuteScalarAsync())!;
            if (count > 0)
            {
                _logger.LogWarning("Platform signup failed: email {Email} already exists.", email);
                return AuthResult.Fail("Email already registered.");
            }
        }

        var userId = Guid.NewGuid();
        var passwordHash = _passwordHasher.Hash(password);

        await using (var insertCmd = new NpgsqlCommand(@"
            INSERT INTO ""platform_users"" (""id"", ""email"", ""password_hash"", ""role"", ""created_at"")
            VALUES (@id, @email, @passwordHash, @role, @createdAt)", conn))
        {
            insertCmd.Parameters.AddWithValue("id", userId);
            insertCmd.Parameters.AddWithValue("email", email);
            insertCmd.Parameters.AddWithValue("passwordHash", passwordHash);
            insertCmd.Parameters.AddWithValue("role", "admin");
            insertCmd.Parameters.AddWithValue("createdAt", DateTime.UtcNow);
            await insertCmd.ExecuteNonQueryAsync();
        }

        var token = _jwtService.GeneratePlatformToken(userId, "admin");
        _logger.LogInformation("Platform user {UserId} signed up.", userId);

        return AuthResult.Ok(token, userId, "admin");
    }

    public async Task<AuthResult> LoginAsync(string email, string password)
    {
        await using var conn = await OpenConnectionAsync();

        await using var cmd = new NpgsqlCommand(@"
            SELECT ""id"", ""password_hash"", ""role""
            FROM ""platform_users""
            WHERE ""email"" = @email", conn);

        cmd.Parameters.AddWithValue("email", email);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            _logger.LogWarning("Platform login failed: email {Email} not found.", email);
            return AuthResult.Fail("Invalid email or password.");
        }

        var userId = reader.GetGuid(0);
        var hash = reader.GetString(1);
        var role = reader.GetString(2);

        if (!_passwordHasher.Verify(password, hash))
        {
            _logger.LogWarning("Platform login failed: invalid password for user {UserId}.", userId);
            return AuthResult.Fail("Invalid email or password.");
        }

        var token = _jwtService.GeneratePlatformToken(userId, role);
        _logger.LogInformation("Platform user {UserId} logged in.", userId);

        return AuthResult.Ok(token, userId, role);
    }

    public async Task EnsurePlatformTablesAsync()
    {
        await using var conn = await OpenConnectionAsync();

        var sql = @"
            CREATE TABLE IF NOT EXISTS ""platform_users"" (
                ""id"" uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                ""email"" varchar(255) UNIQUE NOT NULL,
                ""password_hash"" varchar(255) NOT NULL,
                ""role"" varchar(50) NOT NULL DEFAULT 'admin',
                ""created_at"" timestamptz DEFAULT now()
            );
            ALTER TABLE ""projects"" ADD COLUMN IF NOT EXISTS ""owner_user_id"" uuid;
            ALTER TABLE ""projects"" DROP CONSTRAINT IF EXISTS ""projects_owner_user_id_fkey"";
            CREATE TABLE IF NOT EXISTS ""project_api_keys"" (
                ""id"" uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                ""project_id"" uuid NOT NULL REFERENCES ""projects""(""id"") ON DELETE CASCADE,
                ""key_type"" varchar(20) NOT NULL,
                ""key_hash"" varchar(64) NOT NULL UNIQUE,
                ""key_prefix"" varchar(32) NOT NULL,
                ""name"" varchar(100),
                ""created_at"" timestamptz NOT NULL DEFAULT now(),
                ""last_used_at"" timestamptz,
                ""revoked_at"" timestamptz
            );";

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
