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
        Dictionary<string, string> queryParams,
        string? rowFilter = null)
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
                ApplyRowFilter(query, rowFilter);
                return await ExecuteQueryAsync(conn, query);

            case "read":
                var readId = pathParams.Values.FirstOrDefault() ?? "";
                query = _queryBuilder.BuildRead(entity, readId);
                ApplyRowFilter(query, rowFilter);
                var rows = await ExecuteQueryAsync(conn, query);
                return rows.FirstOrDefault();

            case "create":
                query = _queryBuilder.BuildCreate(entity, body ?? new());
                var created = await ExecuteQueryAsync(conn, query);
                return created.FirstOrDefault();

            case "update":
                var updateId = pathParams.Values.FirstOrDefault() ?? "";
                query = _queryBuilder.BuildUpdate(entity, updateId, body ?? new());
                ApplyRowFilter(query, rowFilter);
                var updated = await ExecuteQueryAsync(conn, query);
                return updated.FirstOrDefault();

            case "delete":
            {
                var deleteId = pathParams.Values.FirstOrDefault() ?? "";
                query = _queryBuilder.BuildDelete(entity, deleteId);
                ApplyRowFilter(query, rowFilter);
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

    private static void ApplyRowFilter(SqlQuery query, string? rowFilter)
    {
        if (string.IsNullOrEmpty(rowFilter)) return;

        // Inject the row filter into the WHERE clause
        var sql = query.Sql;
        var whereIndex = sql.IndexOf(" WHERE ", StringComparison.OrdinalIgnoreCase);

        if (whereIndex >= 0)
        {
            // Insert after existing WHERE keyword
            var insertPos = whereIndex + " WHERE ".Length;
            query.Sql = sql.Insert(insertPos, $"({rowFilter}) AND ");
        }
        else
        {
            // Find the right place to insert WHERE (before LIMIT, ORDER BY, RETURNING, etc.)
            var insertBefore = FindInsertPoint(sql);
            if (insertBefore >= 0)
                query.Sql = sql.Insert(insertBefore, $" WHERE {rowFilter}");
            else
                query.Sql = sql + $" WHERE {rowFilter}";
        }
    }

    private static int FindInsertPoint(string sql)
    {
        string[] keywords = [" LIMIT ", " ORDER BY ", " RETURNING "];
        var earliest = -1;
        foreach (var kw in keywords)
        {
            var idx = sql.IndexOf(kw, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0 && (earliest < 0 || idx < earliest))
                earliest = idx;
        }
        return earliest;
    }

    private static NpgsqlCommand BuildCommand(NpgsqlConnection conn, SqlQuery query)
    {
        var cmd = new NpgsqlCommand(query.Sql, conn);
        foreach (var (key, value) in query.Parameters)
            cmd.Parameters.AddWithValue(key, value ?? DBNull.Value);
        return cmd;
    }
}
