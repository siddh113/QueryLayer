using System.Text.RegularExpressions;
using QueryLayer.Api.Models.Runtime;

namespace QueryLayer.Api.Services.Workflow.Conditions;

/// <summary>
/// Converts SQL-like AI subqueries to the safe internal condition DSL syntax.
/// For example:
///   (SELECT state FROM subscriptions WHERE id = subscription_id) == 'retrying_payment'
/// becomes:
///   subscription.state == 'retrying_payment'
/// </summary>
public sealed class ConditionNormalizationService
{
    private readonly RelatedRecordResolver _relatedResolver;

    // Pattern: (SELECT column FROM table WHERE where_field = record_field) op value
    private static readonly Regex SelectSubqueryPattern = new(
        @"\(\s*SELECT\s+(\w+)\s+FROM\s+(\w+)\s+WHERE\s+(\w+)\s*=\s*(\w+)\s*\)\s*(==|!=|>=?|<=?)\s*(.+?)(?=\s+AND\s+|\s+OR\s+|$)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Patterns that indicate unsafe SQL and should be rejected entirely
    private static readonly Regex UnsafePatterns = new(
        @"\b(JOIN|UNION|INSERT|UPDATE|DELETE|DROP|TRUNCATE|ALTER|CREATE|EXEC|EXECUTE|GRANT|REVOKE|MERGE)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex NestedSelectPattern = new(
        @"SELECT\s+.*\bSELECT\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex FunctionCallPattern = new(
        @"\b\w+\s*\([^)]*\)",
        RegexOptions.Compiled);

    public ConditionNormalizationService(RelatedRecordResolver relatedResolver)
    {
        _relatedResolver = relatedResolver;
    }

    /// <summary>
    /// Attempt to normalize a condition string, converting SQL subqueries to DSL syntax.
    /// </summary>
    public NormalizationResult Normalize(string condition, BackendSpec spec, EntitySpec currentEntity)
    {
        if (string.IsNullOrWhiteSpace(condition))
            return new NormalizationResult { NormalizedCondition = condition, WasNormalized = false };

        // Reject conditions with dangerous SQL patterns
        if (condition.Contains(';') || condition.Contains("--") ||
            condition.Contains("/*") || condition.Contains("*/"))
        {
            return new NormalizationResult
            {
                NormalizedCondition = condition,
                WasNormalized = false,
                Warnings = new List<string>
                {
                    "Condition contains disallowed characters (semicolons or SQL comments)."
                }
            };
        }

        if (UnsafePatterns.IsMatch(condition))
        {
            return new NormalizationResult
            {
                NormalizedCondition = condition,
                WasNormalized = false,
                Warnings = new List<string>
                {
                    "Condition contains unsafe SQL keywords."
                }
            };
        }

        if (NestedSelectPattern.IsMatch(condition))
        {
            return new NormalizationResult
            {
                NormalizedCondition = condition,
                WasNormalized = false,
                Warnings = new List<string>
                {
                    "Nested SELECT statements are not supported."
                }
            };
        }

        // Check if there are any SELECT subqueries to normalize
        if (!condition.Contains("SELECT", StringComparison.OrdinalIgnoreCase))
            return new NormalizationResult { NormalizedCondition = condition, WasNormalized = false };

        // Reject function calls (e.g., COALESCE(...), LOWER(...))
        // But allow SELECT subquery patterns which also use parens
        var withoutSelects = SelectSubqueryPattern.Replace(condition, "");
        if (FunctionCallPattern.IsMatch(withoutSelects))
        {
            return new NormalizationResult
            {
                NormalizedCondition = condition,
                WasNormalized = false,
                Warnings = new List<string>
                {
                    "SQL function calls are not supported in conditions."
                }
            };
        }

        var normalized = condition;
        var wasNormalized = false;
        var warnings = new List<string>();

        normalized = SelectSubqueryPattern.Replace(normalized, match =>
        {
            var selectColumn = match.Groups[1].Value;
            var fromTable = match.Groups[2].Value;
            var whereColumn = match.Groups[3].Value;
            var recordField = match.Groups[4].Value;
            var op = match.Groups[5].Value;
            var compareValue = match.Groups[6].Value.Trim();

            // Try to resolve the relation via the spec
            var resolved = TryResolveRelation(fromTable, whereColumn, recordField, currentEntity, spec);
            if (resolved == null)
            {
                warnings.Add(
                    $"Could not resolve SELECT subquery for table '{fromTable}' — " +
                    "no matching foreign key relation found.");
                return match.Value;
            }

            wasNormalized = true;
            return $"{resolved.Alias}.{selectColumn} {op} {compareValue}";
        });

        return new NormalizationResult
        {
            NormalizedCondition = normalized,
            WasNormalized = wasNormalized,
            Warnings = warnings
        };
    }

    private ResolvedRelation? TryResolveRelation(
        string fromTable,
        string whereColumn,
        string recordField,
        EntitySpec currentEntity,
        BackendSpec spec)
    {
        var relations = _relatedResolver.ResolveAll(currentEntity, spec);

        // Find a relation where the related table matches and the join makes sense
        return relations.FirstOrDefault(r =>
            r.RelatedTable.Equals(fromTable, StringComparison.OrdinalIgnoreCase) &&
            r.RelatedColumn.Equals(whereColumn, StringComparison.OrdinalIgnoreCase) &&
            r.ForeignKeyField.Equals(recordField, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class NormalizationResult
{
    public string NormalizedCondition { get; set; } = string.Empty;
    public bool WasNormalized { get; set; }
    public List<string> Warnings { get; set; } = new();
}
