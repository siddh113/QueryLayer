using QueryLayer.Api.Models.Runtime;

namespace QueryLayer.Api.Services.Runtime;

public class SchemaSyncValidator
{
    private readonly SchemaMigrationService _migrationService;
    private readonly SchemaGeneratorService _schemaGenerator;
    private readonly PostgresTypeMapper _typeMapper;
    private readonly ILogger<SchemaSyncValidator> _logger;

    private static readonly Dictionary<string, string> PgTypeNormalization = new(StringComparer.OrdinalIgnoreCase)
    {
        ["character varying"] = "varchar",
        ["integer"] = "int",
        ["boolean"] = "boolean",
        ["uuid"] = "uuid",
        ["timestamp with time zone"] = "timestamptz",
        ["text"] = "text"
    };

    public SchemaSyncValidator(
        SchemaMigrationService migrationService,
        SchemaGeneratorService schemaGenerator,
        PostgresTypeMapper typeMapper,
        ILogger<SchemaSyncValidator> logger)
    {
        _migrationService = migrationService;
        _schemaGenerator = schemaGenerator;
        _typeMapper = typeMapper;
        _logger = logger;
    }

    public async Task<SchemaSyncResult> ValidateAsync(List<EntitySpec> entities)
    {
        var result = new SchemaSyncResult();

        foreach (var entity in entities)
        {
            var table = entity.Table.ToLowerInvariant();
            var tableExists = await _migrationService.TableExistsAsync(table);

            if (!tableExists)
            {
                result.MissingTables.Add(table);
                _logger.LogWarning("Table '{Table}' does not exist in database.", table);
                continue;
            }

            var existingColumns = await _migrationService.GetExistingColumnsAsync(table);
            var existingColNames = existingColumns.Select(c => c.ColumnName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var specColNames = entity.Fields.Select(f => f.Name.ToLowerInvariant()).ToHashSet(StringComparer.OrdinalIgnoreCase);

            // New fields in spec not in DB
            foreach (var field in entity.Fields)
            {
                var colName = field.Name.ToLowerInvariant();
                if (!existingColNames.Contains(colName))
                {
                    result.NewColumns.Add(new ColumnDiff { Table = table, Column = colName, Detail = "Missing in database" });
                    _logger.LogWarning("Column '{Column}' in entity '{Entity}' is missing from table '{Table}'.", colName, entity.Name, table);
                }
            }

            // Columns in DB not in spec
            foreach (var col in existingColumns)
            {
                if (!specColNames.Contains(col.ColumnName))
                {
                    result.ExtraColumns.Add(new ColumnDiff { Table = table, Column = col.ColumnName, Detail = "Exists in database but not in spec" });
                    _logger.LogWarning("Column '{Column}' in table '{Table}' is not defined in the spec.", col.ColumnName, table);
                }
            }

            // Type mismatches
            foreach (var field in entity.Fields)
            {
                var colName = field.Name.ToLowerInvariant();
                var existing = existingColumns.FirstOrDefault(c =>
                    c.ColumnName.Equals(colName, StringComparison.OrdinalIgnoreCase));
                if (existing == null) continue;

                var expectedType = _typeMapper.Map(field.Type).ToLowerInvariant();
                var actualType = NormalizePgType(existing.DataType);

                if (!expectedType.StartsWith(actualType) && !actualType.StartsWith(expectedType.Split('(')[0]))
                {
                    result.TypeMismatches.Add(new ColumnDiff
                    {
                        Table = table,
                        Column = colName,
                        Detail = $"Expected '{expectedType}', got '{existing.DataType}'"
                    });
                    _logger.LogWarning("Type mismatch for '{Table}'.'{Column}': expected '{Expected}', got '{Actual}'.",
                        table, colName, expectedType, existing.DataType);
                }
            }
        }

        result.IsInSync = result.MissingTables.Count == 0
                          && result.NewColumns.Count == 0
                          && result.ExtraColumns.Count == 0
                          && result.TypeMismatches.Count == 0;

        return result;
    }

    public async Task<List<string>> GenerateSyncStatements(List<EntitySpec> entities)
    {
        var statements = new List<string>();
        var validation = await ValidateAsync(entities);

        // Create missing tables
        foreach (var table in validation.MissingTables)
        {
            var entity = entities.First(e => e.Table.Equals(table, StringComparison.OrdinalIgnoreCase));
            statements.Add(_schemaGenerator.GenerateCreateTable(entity));
            statements.AddRange(_schemaGenerator.GenerateIndexes(entity));
        }

        // Add missing columns
        foreach (var col in validation.NewColumns)
        {
            var entity = entities.First(e => e.Table.Equals(col.Table, StringComparison.OrdinalIgnoreCase));
            var field = entity.Fields.First(f => f.Name.Equals(col.Column, StringComparison.OrdinalIgnoreCase));
            statements.AddRange(_schemaGenerator.GenerateAddColumns(col.Table, new List<FieldSpec> { field }));
        }

        // Alter type mismatches
        foreach (var col in validation.TypeMismatches)
        {
            var entity = entities.First(e => e.Table.Equals(col.Table, StringComparison.OrdinalIgnoreCase));
            var field = entity.Fields.First(f => f.Name.Equals(col.Column, StringComparison.OrdinalIgnoreCase));
            statements.AddRange(_schemaGenerator.GenerateAlterColumnTypes(col.Table, new List<FieldSpec> { field }));
        }

        return statements;
    }

    private static string NormalizePgType(string pgType)
    {
        if (PgTypeNormalization.TryGetValue(pgType, out var normalized))
            return normalized;
        return pgType.ToLowerInvariant();
    }
}

public class SchemaSyncResult
{
    public bool IsInSync { get; set; }
    public List<string> MissingTables { get; set; } = new();
    public List<ColumnDiff> NewColumns { get; set; } = new();
    public List<ColumnDiff> ExtraColumns { get; set; } = new();
    public List<ColumnDiff> TypeMismatches { get; set; } = new();
}

public class ColumnDiff
{
    public string Table { get; set; } = string.Empty;
    public string Column { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}
