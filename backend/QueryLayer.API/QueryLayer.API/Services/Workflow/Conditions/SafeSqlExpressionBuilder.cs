using System.Text.RegularExpressions;

namespace QueryLayer.Api.Services.Workflow.Conditions;

/// <summary>
/// Helper for building SQL fragments with safety guarantees.
/// All identifiers are validated. All values are parameterized. Dangerous keywords are blocked.
/// </summary>
public static class SafeSqlExpressionBuilder
{
    private static readonly Regex IdentifierPattern = new(
        @"^[a-zA-Z_][a-zA-Z0-9_]*$",
        RegexOptions.Compiled);

    private static readonly HashSet<string> BlockedKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "DROP", "DELETE", "INSERT", "UPDATE", "UNION", "TRUNCATE", "ALTER", "CREATE",
        "EXEC", "EXECUTE", "GRANT", "REVOKE", "MERGE", "CALL", "COPY"
    };

    private static readonly HashSet<string> SqlReservedWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT", "FROM", "WHERE", "AND", "OR", "NOT", "NULL", "TRUE", "FALSE",
        "IN", "IS", "LIKE", "BETWEEN", "AS", "ON", "JOIN", "LEFT", "RIGHT",
        "INNER", "OUTER", "CROSS", "ORDER", "BY", "GROUP", "HAVING", "LIMIT",
        "OFFSET", "DISTINCT", "ALL", "ANY", "SOME", "EXISTS", "CASE", "WHEN",
        "THEN", "ELSE", "END", "ASC", "DESC", "INTO", "VALUES", "SET",
        "TABLE", "INDEX", "VIEW", "PRIMARY", "KEY", "FOREIGN", "REFERENCES",
        "CHECK", "CONSTRAINT", "DEFAULT", "CASCADE", "RESTRICT"
    };

    /// <summary>
    /// Validate that a name is a safe SQL identifier (letters, digits, underscores only,
    /// starts with letter or underscore, and is not a blocked keyword).
    /// </summary>
    public static bool ValidateIdentifier(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        if (!IdentifierPattern.IsMatch(name))
            return false;

        if (BlockedKeywords.Contains(name))
            return false;

        return true;
    }

    /// <summary>
    /// Validate identifier, throwing if invalid.
    /// </summary>
    public static string RequireValidIdentifier(string name, string context)
    {
        if (!ValidateIdentifier(name))
            throw new ConditionCompileException(
                $"Invalid SQL identifier '{name}' in {context}. " +
                "Identifiers must match [a-zA-Z_][a-zA-Z0-9_]* and must not be a blocked keyword.");
        return name;
    }

    /// <summary>
    /// Build a simple comparison expression: leftSql op @paramName.
    /// </summary>
    public static string BuildComparison(string leftSql, string op, string rightParam)
    {
        ValidateOperator(op);
        return $"{leftSql} {MapOperator(op)} {rightParam}";
    }

    /// <summary>
    /// Build an EXISTS subquery.
    /// </summary>
    public static string BuildExists(string table, string whereSql)
    {
        RequireValidIdentifier(table, "EXISTS table");
        return $"EXISTS (SELECT 1 FROM {table} WHERE {whereSql})";
    }

    /// <summary>
    /// Build a COUNT subquery with a comparison.
    /// </summary>
    public static string BuildCount(string table, string whereSql, string op, string countParam)
    {
        RequireValidIdentifier(table, "COUNT table");
        ValidateOperator(op);
        return $"(SELECT COUNT(*) FROM {table} WHERE {whereSql}) {MapOperator(op)} {countParam}";
    }

    /// <summary>
    /// Build a scalar LOOKUP subquery with a comparison.
    /// </summary>
    public static string BuildLookup(string table, string selectField, string whereSql, string op, string compareParam)
    {
        RequireValidIdentifier(table, "LOOKUP table");
        RequireValidIdentifier(selectField, "LOOKUP select field");
        ValidateOperator(op);
        return $"(SELECT {selectField} FROM {table} WHERE {whereSql} LIMIT 1) {MapOperator(op)} {compareParam}";
    }

    /// <summary>
    /// Check a raw condition string for SQL injection patterns. Returns the list of detected threats.
    /// </summary>
    public static List<string> DetectInjectionPatterns(string condition)
    {
        var threats = new List<string>();

        if (condition.Contains(';'))
            threats.Add("Semicolons are not allowed in conditions.");

        if (condition.Contains("--"))
            threats.Add("SQL line comments (--) are not allowed in conditions.");

        if (condition.Contains("/*") || condition.Contains("*/"))
            threats.Add("SQL block comments (/* */) are not allowed in conditions.");

        foreach (var keyword in BlockedKeywords)
        {
            // Match whole-word only to avoid false positives
            if (Regex.IsMatch(condition, $@"\b{keyword}\b", RegexOptions.IgnoreCase))
                threats.Add($"Blocked SQL keyword '{keyword}' detected in condition.");
        }

        return threats;
    }

    /// <summary>
    /// Map DSL comparison operators to SQL operators.
    /// </summary>
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

    private static void ValidateOperator(string op)
    {
        if (op is not ("==" or "!=" or ">" or "<" or ">=" or "<="))
            throw new ConditionCompileException($"Unsupported comparison operator '{op}'.");
    }
}

/// <summary>
/// Thrown when a condition AST cannot be compiled to SQL.
/// </summary>
public sealed class ConditionCompileException : Exception
{
    public ConditionCompileException(string message) : base(message) { }
}
