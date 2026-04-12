using System.Text;
using System.Text.RegularExpressions;
using QueryLayer.Api.Models.Runtime;

namespace QueryLayer.Api.Services.Runtime;

public class SchemaGeneratorService
{
    private static readonly Regex ValidIdentifier = new(@"^[a-zA-Z_][a-zA-Z0-9_]*$");
    private readonly PostgresTypeMapper _typeMapper;
    private readonly ILogger<SchemaGeneratorService> _logger;

    public SchemaGeneratorService(PostgresTypeMapper typeMapper, ILogger<SchemaGeneratorService> logger)
    {
        _typeMapper = typeMapper;
        _logger = logger;
    }

    public List<string> GenerateCreateStatements(List<EntitySpec> entities)
    {
        var statements = new List<string>();

        foreach (var entity in entities)
        {
            statements.Add(GenerateCreateTable(entity));
            statements.AddRange(GenerateIndexes(entity));
        }

        return statements;
    }

    public string GenerateCreateTable(EntitySpec entity)
    {
        var table = ValidateId(entity.Table);
        var sb = new StringBuilder();
        sb.AppendLine($"CREATE TABLE IF NOT EXISTS \"{table}\" (");

        var columnDefs = new List<string>();
        var constraints = new List<string>();

        foreach (var field in entity.Fields)
        {
            var col = ValidateId(field.Name);
            var pgType = _typeMapper.Map(field.Type);
            var parts = new List<string> { $"  \"{col}\" {pgType}" };

            if (field.Primary)
            {
                if (field.Type.Equals("uuid", StringComparison.OrdinalIgnoreCase))
                    parts.Add("DEFAULT gen_random_uuid()");
                parts.Add("PRIMARY KEY");
            }

            if (field.Required && !field.Primary)
                parts.Add("NOT NULL");

            if (field.Unique && !field.Primary)
                parts.Add("UNIQUE");

            columnDefs.Add(string.Join(" ", parts));

            if (field.Relation != null)
            {
                var refTable = ValidateId(field.Relation.Table);
                var refCol = ValidateId(field.Relation.Column);
                constraints.Add($"  FOREIGN KEY (\"{col}\") REFERENCES \"{refTable}\"(\"{refCol}\")");
            }
        }

        sb.AppendLine(string.Join(",\n", columnDefs.Concat(constraints)));
        sb.Append(");");

        var sql = sb.ToString();
        _logger.LogInformation("Generated CREATE TABLE for {Table}: {Sql}", table, sql);
        return sql;
    }

    public List<string> GenerateIndexes(EntitySpec entity)
    {
        var table = ValidateId(entity.Table);
        var indexes = new List<string>();

        foreach (var field in entity.Fields)
        {
            if (field.Primary) continue;

            var col = ValidateId(field.Name);
            var needsIndex = field.Relation != null || field.Unique;

            if (needsIndex)
            {
                var indexName = $"idx_{table}_{col}";
                var unique = field.Unique ? "UNIQUE " : "";
                indexes.Add($"CREATE {unique}INDEX IF NOT EXISTS \"{indexName}\" ON \"{table}\" (\"{col}\");");
            }
        }

        return indexes;
    }

    public List<string> GenerateAddColumns(string table, List<FieldSpec> newFields)
    {
        var t = ValidateId(table);
        var statements = new List<string>();

        foreach (var field in newFields)
        {
            var col = ValidateId(field.Name);
            var pgType = _typeMapper.Map(field.Type);
            var sql = $"ALTER TABLE \"{t}\" ADD COLUMN IF NOT EXISTS \"{col}\" {pgType}";

            if (field.Required)
                sql += " NOT NULL DEFAULT ''";
            if (field.Unique)
                sql += " UNIQUE";

            sql += ";";
            statements.Add(sql);

            if (field.Relation != null)
            {
                var refTable = ValidateId(field.Relation.Table);
                var refCol = ValidateId(field.Relation.Column);
                statements.Add(
                    $"ALTER TABLE \"{t}\" ADD CONSTRAINT \"fk_{t}_{col}\" FOREIGN KEY (\"{col}\") REFERENCES \"{refTable}\"(\"{refCol}\");");
            }
        }

        return statements;
    }

    public List<string> GenerateDropColumns(string table, List<string> columnNames)
    {
        var t = ValidateId(table);
        return columnNames
            .Select(col => $"ALTER TABLE \"{t}\" DROP COLUMN IF EXISTS \"{ValidateId(col)}\";")
            .ToList();
    }

    public List<string> GenerateAlterColumnTypes(string table, List<FieldSpec> changedFields)
    {
        var t = ValidateId(table);
        return changedFields
            .Select(field =>
            {
                var col = ValidateId(field.Name);
                var pgType = _typeMapper.Map(field.Type);
                return $"ALTER TABLE \"{t}\" ALTER COLUMN \"{col}\" TYPE {pgType} USING \"{col}\"::{pgType};";
            })
            .ToList();
    }

    private static string ValidateId(string name)
    {
        if (!ValidIdentifier.IsMatch(name))
            throw new InvalidOperationException($"Invalid identifier: '{name}'");
        return name.ToLowerInvariant();
    }
}
