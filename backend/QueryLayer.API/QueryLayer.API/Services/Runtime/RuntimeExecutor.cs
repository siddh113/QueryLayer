using Npgsql;
using QueryLayer.Api.Models.Runtime;

namespace QueryLayer.Api.Services.Runtime;

public class RuntimeExecutor
{
    private readonly IConfiguration _configuration;
    private readonly SqlQueryBuilder _queryBuilder;

    public RuntimeExecutor(IConfiguration configuration, SqlQueryBuilder queryBuilder)
    {
        _configuration = configuration;
        _queryBuilder = queryBuilder;
    }

    public async Task<object?> ExecuteAsync(
        EndpointSpec endpoint,
        EntitySpec entity,
        Dictionary<string, string> pathParams,
        Dictionary<string, object?>? body,
        Dictionary<string, string> queryParams)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("DATABASE_URL") ??
            _configuration.GetConnectionString("DefaultConnection");

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        SqlQuery query;

        switch (endpoint.Operation.ToLowerInvariant())
        {
            case "list":
                var filters = queryParams
                    .Where(q => q.Key != "page" && q.Key != "limit")
                    .ToDictionary(q => q.Key, q => q.Value);
                int.TryParse(queryParams.GetValueOrDefault("page", "1"), out var page);
                int.TryParse(queryParams.GetValueOrDefault("limit", "20"), out var limit);
                if (page < 1) page = 1;
                if (limit < 1 || limit > 100) limit = 20;
                query = _queryBuilder.BuildList(entity, filters, page, limit);
                return await ExecuteQueryAsync(conn, query);

            case "read":
                var readId = pathParams.Values.FirstOrDefault() ?? "";
                query = _queryBuilder.BuildRead(entity, readId);
                var rows = await ExecuteQueryAsync(conn, query);
                return rows.FirstOrDefault();

            case "create":
                query = _queryBuilder.BuildCreate(entity, body ?? new());
                var created = await ExecuteQueryAsync(conn, query);
                return created.FirstOrDefault();

            case "update":
                var updateId = pathParams.Values.FirstOrDefault() ?? "";
                query = _queryBuilder.BuildUpdate(entity, updateId, body ?? new());
                var updated = await ExecuteQueryAsync(conn, query);
                return updated.FirstOrDefault();

            case "delete":
            {
                var deleteId = pathParams.Values.FirstOrDefault() ?? "";
                query = _queryBuilder.BuildDelete(entity, deleteId);
                await using var deleteCmd = BuildCommand(conn, query);
                await deleteCmd.ExecuteNonQueryAsync();
                return new { message = "Deleted successfully" };
            }

            default:
                throw new InvalidOperationException($"Unknown operation: {endpoint.Operation}");
        }
    }

    private static async Task<List<Dictionary<string, object?>>> ExecuteQueryAsync(
        NpgsqlConnection conn, SqlQuery query)
    {
        await using var cmd = BuildCommand(conn, query);
        await using var reader = await cmd.ExecuteReaderAsync();

        var results = new List<Dictionary<string, object?>>();
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            results.Add(row);
        }
        return results;
    }

    private static NpgsqlCommand BuildCommand(NpgsqlConnection conn, SqlQuery query)
    {
        var cmd = new NpgsqlCommand(query.Sql, conn);
        foreach (var (key, value) in query.Parameters)
            cmd.Parameters.AddWithValue(key, value ?? DBNull.Value);
        return cmd;
    }
}
