using Npgsql;

namespace QueryLayer.Api.Services.Runtime;

public class SchemaMigrationService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SchemaMigrationService> _logger;

    public SchemaMigrationService(IConfiguration configuration, ILogger<SchemaMigrationService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task ExecuteAsync(List<string> sqlStatements)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("DATABASE_URL") ??
            _configuration.GetConnectionString("DefaultConnection");

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        await using var transaction = await conn.BeginTransactionAsync();

        try
        {
            foreach (var sql in sqlStatements)
            {
                _logger.LogInformation("Executing migration SQL: {Sql}", sql);
                await using var cmd = new NpgsqlCommand(sql, conn, transaction);
                await cmd.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
            _logger.LogInformation("Migration completed successfully. {Count} statements executed.", sqlStatements.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Migration failed. Rolling back transaction.");
            await transaction.RollbackAsync();
            throw new InvalidOperationException("Schema migration failed", ex);
        }
    }

    public async Task<List<TableColumnInfo>> GetExistingColumnsAsync(string tableName)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("DATABASE_URL") ??
            _configuration.GetConnectionString("DefaultConnection");

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        var sql = @"
            SELECT column_name, data_type, is_nullable,
                   character_maximum_length
            FROM information_schema.columns
            WHERE table_name = @tableName
            AND table_schema = 'public'
            ORDER BY ordinal_position;";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tableName", tableName);

        var columns = new List<TableColumnInfo>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(new TableColumnInfo
            {
                ColumnName = reader.GetString(0),
                DataType = reader.GetString(1),
                IsNullable = reader.GetString(2) == "YES",
                MaxLength = reader.IsDBNull(3) ? null : reader.GetInt32(3)
            });
        }

        return columns;
    }

    public async Task<bool> TableExistsAsync(string tableName)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("DATABASE_URL") ??
            _configuration.GetConnectionString("DefaultConnection");

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        var sql = @"
            SELECT EXISTS (
                SELECT FROM information_schema.tables
                WHERE table_schema = 'public'
                AND table_name = @tableName
            );";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tableName", tableName);
        var result = await cmd.ExecuteScalarAsync();
        return result is true;
    }
}

public class TableColumnInfo
{
    public string ColumnName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsNullable { get; set; }
    public int? MaxLength { get; set; }
}
