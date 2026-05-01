namespace QueryLayer.Api.Services.Workflow;

public class ConditionEvaluator
{
    // Supports: field > value, field < value, field == value, field != value
    // Compound: expr1 AND expr2
    // Previous record: previous.field == value
    public bool Evaluate(string? condition, Dictionary<string, object?> record,
        Dictionary<string, object?>? previousRecord = null)
    {
        if (string.IsNullOrWhiteSpace(condition))
            return true;

        condition = condition.Trim();

        // Split on AND (case-insensitive)
        var parts = SplitOnAnd(condition);
        return parts.All(part => EvaluateSingle(part.Trim(), record, previousRecord));
    }

    private static List<string> SplitOnAnd(string condition)
    {
        // Split on " AND " (case-insensitive), respecting quoted strings
        var parts = new List<string>();
        var current = 0;
        while (current < condition.Length)
        {
            var idx = condition.IndexOf(" AND ", current, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                parts.Add(condition[current..]);
                break;
            }
            parts.Add(condition[current..idx]);
            current = idx + 5; // " AND ".Length
        }
        return parts;
    }

    private static bool EvaluateSingle(string condition, Dictionary<string, object?> record,
        Dictionary<string, object?>? previousRecord)
    {
        string[] operators = ["!=", "==", ">=", "<=", ">", "<"];
        foreach (var op in operators)
        {
            var idx = condition.IndexOf(op, StringComparison.Ordinal);
            if (idx < 0) continue;

            var field = condition[..idx].Trim();
            var rawValue = condition[(idx + op.Length)..].Trim().Trim('"').Trim('\'');

            object? recordVal;
            if (field.StartsWith("previous.", StringComparison.OrdinalIgnoreCase))
            {
                var prevField = field["previous.".Length..];
                if (previousRecord == null || !previousRecord.TryGetValue(prevField, out recordVal))
                    return false;
            }
            else
            {
                if (!record.TryGetValue(field, out recordVal))
                    return false;
            }

            return Compare(recordVal, op, rawValue);
        }

        return false;
    }

    private static bool Compare(object? recordVal, string op, string rawValue)
    {
        // Try numeric comparison first
        if (TryParseDecimal(recordVal, out var numRecord) &&
            decimal.TryParse(rawValue, out var numExpected))
        {
            return op switch
            {
                ">" => numRecord > numExpected,
                "<" => numRecord < numExpected,
                ">=" => numRecord >= numExpected,
                "<=" => numRecord <= numExpected,
                "==" => numRecord == numExpected,
                "!=" => numRecord != numExpected,
                _ => false
            };
        }

        // String comparison
        var strRecord = recordVal?.ToString() ?? "";
        return op switch
        {
            "==" => string.Equals(strRecord, rawValue, StringComparison.OrdinalIgnoreCase),
            "!=" => !string.Equals(strRecord, rawValue, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static bool TryParseDecimal(object? val, out decimal result)
    {
        result = 0;
        if (val == null) return false;
        return decimal.TryParse(val.ToString(), out result);
    }
}
