using System.Text.RegularExpressions;

namespace QueryLayer.Api.Services.AI;

public class ConditionSyntaxValidator
{
    private static readonly HashSet<string> AllowedAuthRefs = new(StringComparer.OrdinalIgnoreCase)
    {
        "auth.role", "auth.user_id"
    };

    // Matches: fieldRef operator value (string|number|bool)
    private static readonly Regex ComparisonRegex = new(
        @"([\w][\w\.]*)\s*(==|!=|>=|<=|>|<)\s*('[^']*'|-?\d+(?:\.\d+)?|true|false)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public (bool IsValid, string? Error) Validate(string condition, IEnumerable<string> knownFields)
    {
        if (string.IsNullOrWhiteSpace(condition))
            return (false, "Condition cannot be empty.");

        var fieldSet = new HashSet<string>(knownFields, StringComparer.OrdinalIgnoreCase);
        var comparisons = ComparisonRegex.Matches(condition);

        if (comparisons.Count == 0)
            return (false, $"Condition '{condition}' contains no valid comparison expressions.");

        foreach (Match match in comparisons)
        {
            var fieldRef = match.Groups[1].Value;

            if (AllowedAuthRefs.Contains(fieldRef))
                continue;

            if (fieldRef.StartsWith("previous.", StringComparison.OrdinalIgnoreCase))
            {
                var actualField = fieldRef[9..]; // skip "previous."
                if (fieldSet.Count > 0 && !fieldSet.Contains(actualField))
                    return (false, $"Condition references unknown field 'previous.{actualField}'.");
                continue;
            }

            // Related-record references (alias.field, e.g. subscription.state) contain a dot.
            // These are validated by ConditionValidator which understands relation metadata.
            if (fieldRef.Contains('.'))
                continue;

            if (fieldSet.Count > 0 && !fieldSet.Contains(fieldRef))
                return (false, $"Condition references unknown field '{fieldRef}'.");
        }

        return (true, null);
    }
}
