using QueryLayer.Api.Models.Runtime;

namespace QueryLayer.Api.Services.Workflow.Conditions;

/// <summary>
/// Compiles a condition AST into a parameterized SQL fragment.
/// All literal values become parameters. All identifiers are validated before inclusion.
/// </summary>
public sealed class ConditionCompiler
{
    private readonly RelatedRecordResolver _relatedResolver;

    public ConditionCompiler(RelatedRecordResolver relatedResolver)
    {
        _relatedResolver = relatedResolver;
    }

    /// <summary>
    /// Compile an AST node into a parameterized SQL fragment.
    /// </summary>
    public CompileResult Compile(
        ConditionNode node,
        string currentTable,
        BackendSpec spec,
        EntitySpec currentEntity)
    {
        SafeSqlExpressionBuilder.RequireValidIdentifier(currentTable, "current table");

        var parameters = new Dictionary<string, object>();
        var paramIndex = 0;
        var sql = CompileNode(node, currentTable, spec, currentEntity, parameters, ref paramIndex);

        return new CompileResult
        {
            SqlFragment = sql,
            Parameters = parameters
        };
    }

    private string CompileNode(
        ConditionNode node,
        string currentTable,
        BackendSpec spec,
        EntitySpec currentEntity,
        Dictionary<string, object> parameters,
        ref int paramIndex)
    {
        return node switch
        {
            ComparisonNode comp => CompileComparison(comp, currentTable, spec, currentEntity, parameters, ref paramIndex),
            LogicalNode logical => CompileLogical(logical, currentTable, spec, currentEntity, parameters, ref paramIndex),
            NotNode not => CompileNot(not, currentTable, spec, currentEntity, parameters, ref paramIndex),
            ExistsNode exists => CompileExists(exists, currentTable, spec, currentEntity, parameters, ref paramIndex),
            CountNode count => CompileCount(count, currentTable, spec, currentEntity, parameters, ref paramIndex),
            LookupNode lookup => CompileLookup(lookup, currentTable, spec, currentEntity, parameters, ref paramIndex),
            _ => throw new ConditionCompileException($"Cannot compile node type '{node.GetType().Name}' at top level; expected a comparison or logical expression.")
        };
    }

    private string CompileComparison(
        ComparisonNode comp,
        string currentTable,
        BackendSpec spec,
        EntitySpec currentEntity,
        Dictionary<string, object> parameters,
        ref int paramIndex)
    {
        var leftSql = CompileValueNode(comp.Left, currentTable, spec, currentEntity, parameters, ref paramIndex);
        var rightSql = CompileValueNode(comp.Right, currentTable, spec, currentEntity, parameters, ref paramIndex);

        var sqlOp = MapOperator(comp.Operator);
        return $"{leftSql} {sqlOp} {rightSql}";
    }

    private string CompileLogical(
        LogicalNode logical,
        string currentTable,
        BackendSpec spec,
        EntitySpec currentEntity,
        Dictionary<string, object> parameters,
        ref int paramIndex)
    {
        var leftSql = CompileNode(logical.Left, currentTable, spec, currentEntity, parameters, ref paramIndex);
        var rightSql = CompileNode(logical.Right, currentTable, spec, currentEntity, parameters, ref paramIndex);

        return $"({leftSql} {logical.Operator} {rightSql})";
    }

    private string CompileNot(
        NotNode not,
        string currentTable,
        BackendSpec spec,
        EntitySpec currentEntity,
        Dictionary<string, object> parameters,
        ref int paramIndex)
    {
        var innerSql = CompileNode(not.Inner, currentTable, spec, currentEntity, parameters, ref paramIndex);
        return $"NOT ({innerSql})";
    }

    private string CompileExists(
        ExistsNode exists,
        string currentTable,
        BackendSpec spec,
        EntitySpec currentEntity,
        Dictionary<string, object> parameters,
        ref int paramIndex)
    {
        SafeSqlExpressionBuilder.RequireValidIdentifier(exists.Table, "EXISTS table");

        var filterSql = CompileFilterList(exists.Filters, exists.Table, currentTable, spec, currentEntity, parameters, ref paramIndex);
        return SafeSqlExpressionBuilder.BuildExists(exists.Table, filterSql);
    }

    private string CompileCount(
        CountNode count,
        string currentTable,
        BackendSpec spec,
        EntitySpec currentEntity,
        Dictionary<string, object> parameters,
        ref int paramIndex)
    {
        SafeSqlExpressionBuilder.RequireValidIdentifier(count.Table, "COUNT table");

        var filterSql = CompileFilterList(count.Filters, count.Table, currentTable, spec, currentEntity, parameters, ref paramIndex);
        var countParam = NextParam(ref paramIndex);
        parameters[countParam] = count.Value;

        return SafeSqlExpressionBuilder.BuildCount(count.Table, filterSql, count.Operator, $"@{countParam}");
    }

    private string CompileLookup(
        LookupNode lookup,
        string currentTable,
        BackendSpec spec,
        EntitySpec currentEntity,
        Dictionary<string, object> parameters,
        ref int paramIndex)
    {
        SafeSqlExpressionBuilder.RequireValidIdentifier(lookup.Table, "LOOKUP table");
        SafeSqlExpressionBuilder.RequireValidIdentifier(lookup.SelectField, "LOOKUP select field");
        SafeSqlExpressionBuilder.RequireValidIdentifier(lookup.WhereField, "LOOKUP where field");

        var whereValueSql = CompileValueNode(lookup.WhereValue, currentTable, spec, currentEntity, parameters, ref paramIndex);
        var whereSql = $"{lookup.WhereField} = {whereValueSql}";

        var compareValueSql = CompileValueNode(lookup.CompareValue, currentTable, spec, currentEntity, parameters, ref paramIndex);
        var sqlOp = MapOperator(lookup.Operator);

        return $"(SELECT {lookup.SelectField} FROM {lookup.Table} WHERE {whereSql} LIMIT 1) {sqlOp} {compareValueSql}";
    }

    private string CompileFilterList(
        List<ComparisonNode> filters,
        string filterTable,
        string currentTable,
        BackendSpec spec,
        EntitySpec currentEntity,
        Dictionary<string, object> parameters,
        ref int paramIndex)
    {
        var parts = new List<string>();
        foreach (var filter in filters)
        {
            var leftSql = CompileFilterValueNode(filter.Left, filterTable, currentTable, spec, currentEntity, parameters, ref paramIndex);
            var rightSql = CompileFilterValueNode(filter.Right, filterTable, currentTable, spec, currentEntity, parameters, ref paramIndex);
            var sqlOp = MapOperator(filter.Operator);
            parts.Add($"{leftSql} {sqlOp} {rightSql}");
        }
        return string.Join(" AND ", parts);
    }

    /// <summary>
    /// Compile a value node that appears inside an EXISTS/COUNT filter.
    /// Bare field references resolve to the filter table, while record.* resolves to the current table.
    /// </summary>
    private string CompileFilterValueNode(
        ConditionNode node,
        string filterTable,
        string currentTable,
        BackendSpec spec,
        EntitySpec currentEntity,
        Dictionary<string, object> parameters,
        ref int paramIndex)
    {
        switch (node)
        {
            case FieldReferenceNode fieldRef:
                SafeSqlExpressionBuilder.RequireValidIdentifier(fieldRef.Field, "filter field");
                return $"{filterTable}.{fieldRef.Field}";

            case RecordFieldReferenceNode recordRef:
                SafeSqlExpressionBuilder.RequireValidIdentifier(recordRef.Field, "record field in filter");
                return $"{currentTable}.{recordRef.Field}";

            case AuthReferenceNode authRef:
                return $"@auth_{authRef.Property}";

            case LiteralNode literal:
                var pName = NextParam(ref paramIndex);
                parameters[pName] = literal.Value;
                return $"@{pName}";

            default:
                return CompileValueNode(node, currentTable, spec, currentEntity, parameters, ref paramIndex);
        }
    }

    /// <summary>
    /// Compile a single value node (field reference, literal, auth, previous, related).
    /// </summary>
    private string CompileValueNode(
        ConditionNode node,
        string currentTable,
        BackendSpec spec,
        EntitySpec currentEntity,
        Dictionary<string, object> parameters,
        ref int paramIndex)
    {
        switch (node)
        {
            case FieldReferenceNode fieldRef:
                SafeSqlExpressionBuilder.RequireValidIdentifier(fieldRef.Field, "field reference");
                return $"{currentTable}.{fieldRef.Field}";

            case RecordFieldReferenceNode recordRef:
                SafeSqlExpressionBuilder.RequireValidIdentifier(recordRef.Field, "record field reference");
                return $"{currentTable}.{recordRef.Field}";

            case PreviousFieldReferenceNode prevRef:
                SafeSqlExpressionBuilder.RequireValidIdentifier(prevRef.Field, "previous field reference");
                return $"@prev_{prevRef.Field}";

            case AuthReferenceNode authRef:
                return $"@auth_{authRef.Property}";

            case RelatedFieldReferenceNode relatedRef:
                return CompileRelatedFieldReference(relatedRef, currentTable, spec, currentEntity, parameters, ref paramIndex);

            case LiteralNode literal:
                var paramName = NextParam(ref paramIndex);
                parameters[paramName] = literal.Value;
                return $"@{paramName}";

            default:
                throw new ConditionCompileException(
                    $"Cannot compile node type '{node.GetType().Name}' as a value expression.");
        }
    }

    private string CompileRelatedFieldReference(
        RelatedFieldReferenceNode relatedRef,
        string currentTable,
        BackendSpec spec,
        EntitySpec currentEntity,
        Dictionary<string, object> parameters,
        ref int paramIndex)
    {
        var resolved = _relatedResolver.Resolve(relatedRef.Alias, currentEntity, spec);
        if (resolved == null)
            throw new ConditionCompileException(
                $"Cannot resolve related alias '{relatedRef.Alias}' on entity '{currentEntity.Name}'.");

        SafeSqlExpressionBuilder.RequireValidIdentifier(resolved.RelatedTable, "related table");
        SafeSqlExpressionBuilder.RequireValidIdentifier(relatedRef.Field, "related field");
        SafeSqlExpressionBuilder.RequireValidIdentifier(resolved.RelatedColumn, "related column");
        SafeSqlExpressionBuilder.RequireValidIdentifier(resolved.ForeignKeyField, "foreign key field");

        // Generate correlated subquery: (SELECT field FROM relatedTable WHERE relatedColumn = currentTable.fkField LIMIT 1)
        return $"(SELECT {relatedRef.Field} FROM {resolved.RelatedTable} " +
               $"WHERE {resolved.RelatedColumn} = {currentTable}.{resolved.ForeignKeyField} LIMIT 1)";
    }

    private static string MapOperator(string op)
    {
        return op switch
        {
            "==" => "=",
            "!=" => "!=",
            ">" => ">",
            "<" => "<",
            ">=" => ">=",
            "<=" => "<=",
            _ => throw new ConditionCompileException($"Unsupported operator '{op}'.")
        };
    }

    private static string NextParam(ref int index)
    {
        return $"p{index++}";
    }
}

public sealed class CompileResult
{
    public string SqlFragment { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
}
