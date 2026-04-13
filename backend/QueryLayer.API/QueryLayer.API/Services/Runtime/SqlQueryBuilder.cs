using System.Text.Json;
using System.Text.RegularExpressions;
using QueryLayer.Api.Models.Runtime;

namespace QueryLayer.Api.Services.Runtime;

public class SqlQuery
{
    public string Sql { get; set; } = string.Empty;
    public Dictionary<string, object?> Parameters { get; set; } = new();
}

public class SqlQueryBuilder
{
    private static readonly Regex ValidIdentifier = new(@"^[a-zA-Z_][a-zA-Z0-9_]*$");

    public SqlQuery BuildList(
        EntitySpec entity,
        Dictionary<string, string> filters,
        int page,
        int limit)
    {
        var table = ValidateIdentifier(entity.Table);
        var parameters = new Dictionary<string, object?>();
        var conditions = new List<string>();

        foreach (var (key, value) in filters)
        {
            var field = entity.Fields.FirstOrDefault(f =>
                f.Name.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (field == null) continue;

            var col = ValidateIdentifier(field.Name);
            var param = $"filter_{col}";
            conditions.Add($"\"{col}\" = @{param}");
            parameters[param] = value;
        }

        var where = conditions.Count > 0 ? " WHERE " + string.Join(" AND ", conditions) : "";
        var offset = (page - 1) * limit;
        parameters["limit"] = limit;
        parameters["offset"] = offset;

        return new SqlQuery
        {
            Sql = $"SELECT * FROM \"{table}\"{where} LIMIT @limit OFFSET @offset",
            Parameters = parameters
        };
    }

    public SqlQuery BuildRead(EntitySpec entity, string id)
    {
        var table = ValidateIdentifier(entity.Table);
        var pk = GetPrimaryKey(entity);
        return new SqlQuery
        {
            Sql = $"SELECT * FROM \"{table}\" WHERE \"{pk}\" = @id",
            Parameters = new() { ["id"] = ParseId(entity, id) }
        };
    }

    public SqlQuery BuildCreate(EntitySpec entity, Dictionary<string, object?> body)
    {
        var table = ValidateIdentifier(entity.Table);
        var columns = new List<string>();
        var paramNames = new List<string>();
        var parameters = new Dictionary<string, object?>();

        foreach (var field in entity.Fields)
        {
            if (field.Primary) continue;
            if (!body.ContainsKey(field.Name)) continue;

            var col = ValidateIdentifier(field.Name);
            columns.Add($"\"{col}\"");
            paramNames.Add($"@{col}");
            parameters[col] = ConvertValue(body[field.Name], field.Type);
        }

        return new SqlQuery
        {
            Sql = $"INSERT INTO \"{table}\" ({string.Join(", ", columns)}) VALUES ({string.Join(", ", paramNames)}) RETURNING *",
            Parameters = parameters
        };
    }

    public SqlQuery BuildUpdate(EntitySpec entity, string id, Dictionary<string, object?> body)
    {
        var table = ValidateIdentifier(entity.Table);
        var pk = GetPrimaryKey(entity);
        var setClauses = new List<string>();
        var parameters = new Dictionary<string, object?> { ["id"] = ParseId(entity, id) };

        foreach (var field in entity.Fields)
        {
            if (field.Primary) continue;
            if (!body.ContainsKey(field.Name)) continue;

            var col = ValidateIdentifier(field.Name);
            setClauses.Add($"\"{col}\" = @{col}");
            parameters[col] = ConvertValue(body[field.Name], field.Type);
        }

        if (setClauses.Count == 0)
            throw new InvalidOperationException("No fields to update.");

        return new SqlQuery
        {
            Sql = $"UPDATE \"{table}\" SET {string.Join(", ", setClauses)} WHERE \"{pk}\" = @id RETURNING *",
            Parameters = parameters
        };
    }

    public SqlQuery BuildDelete(EntitySpec entity, string id)
    {
        var table = ValidateIdentifier(entity.Table);
        var pk = GetPrimaryKey(entity);
        return new SqlQuery
        {
            Sql = $"DELETE FROM \"{table}\" WHERE \"{pk}\" = @id",
            Parameters = new() { ["id"] = ParseId(entity, id) }
        };
    }

    private static string GetPrimaryKey(EntitySpec entity)
    {
        var pk = entity.Fields.FirstOrDefault(f => f.Primary);
        return ValidateIdentifier(pk?.Name ?? "id");
    }

    private static object ParseId(EntitySpec entity, string id)
    {
        // Always cast to Guid if the value parses as one — avoids "uuid = text" type errors
        // when the spec field type doesn't exactly match the actual DB column type.
        if (Guid.TryParse(id, out var guid))
            return guid;
        return id;
    }

    private static string ValidateIdentifier(string name)
    {
        if (!ValidIdentifier.IsMatch(name))
            throw new InvalidOperationException($"Invalid identifier: '{name}'");
        return name;
    }

    private static object? ConvertValue(object? value, string type)
    {
        if (value is JsonElement el)
        {
            return type.ToLowerInvariant() switch
            {
                "integer" => el.GetInt64(),
                "boolean" => el.GetBoolean(),
                "uuid" => Guid.Parse(el.GetString()!),
                "timestamp" => DateTime.Parse(el.GetString()!),
                _ => el.GetString()
            };
        }
        return value;
    }
}
