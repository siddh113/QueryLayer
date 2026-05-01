namespace QueryLayer.Api.Services.Workflow.Conditions;

/// <summary>
/// Abstract base for all condition AST nodes.
/// </summary>
public abstract class ConditionNode
{
    public abstract string NodeType { get; }
}

/// <summary>
/// Binary comparison: left op right (e.g., amount > 500, state == 'active').
/// </summary>
public sealed class ComparisonNode : ConditionNode
{
    public override string NodeType => "Comparison";
    public ConditionNode Left { get; }
    public string Operator { get; }
    public ConditionNode Right { get; }

    public ComparisonNode(ConditionNode left, string op, ConditionNode right)
    {
        Left = left;
        Operator = op;
        Right = right;
    }
}

/// <summary>
/// Logical binary operator: AND / OR.
/// </summary>
public sealed class LogicalNode : ConditionNode
{
    public override string NodeType => "Logical";
    public ConditionNode Left { get; }
    public ConditionNode Right { get; }
    public string Operator { get; }

    public LogicalNode(ConditionNode left, string op, ConditionNode right)
    {
        Left = left;
        Operator = op.ToUpperInvariant();
        Right = right;
    }
}

/// <summary>
/// Unary NOT.
/// </summary>
public sealed class NotNode : ConditionNode
{
    public override string NodeType => "Not";
    public ConditionNode Inner { get; }

    public NotNode(ConditionNode inner)
    {
        Inner = inner;
    }
}

/// <summary>
/// Reference to a field on the current record (bare field name, e.g., "amount").
/// </summary>
public sealed class FieldReferenceNode : ConditionNode
{
    public override string NodeType => "FieldReference";
    public string Field { get; }

    public FieldReferenceNode(string field)
    {
        Field = field;
    }
}

/// <summary>
/// Literal value: string, number, or boolean.
/// </summary>
public sealed class LiteralNode : ConditionNode
{
    public override string NodeType => "Literal";
    public object Value { get; }
    public string RawValue { get; }

    public LiteralNode(object value, string rawValue)
    {
        Value = value;
        RawValue = rawValue;
    }
}

/// <summary>
/// Reference to a related entity field via foreign key alias (e.g., subscription.state).
/// </summary>
public sealed class RelatedFieldReferenceNode : ConditionNode
{
    public override string NodeType => "RelatedFieldReference";
    public string Alias { get; }
    public string Field { get; }

    public RelatedFieldReferenceNode(string alias, string field)
    {
        Alias = alias;
        Field = field;
    }
}

/// <summary>
/// Reference to previous record value (e.g., previous.state).
/// </summary>
public sealed class PreviousFieldReferenceNode : ConditionNode
{
    public override string NodeType => "PreviousFieldReference";
    public string Field { get; }

    public PreviousFieldReferenceNode(string field)
    {
        Field = field;
    }
}

/// <summary>
/// Reference to auth context (auth.role, auth.user_id).
/// </summary>
public sealed class AuthReferenceNode : ConditionNode
{
    public override string NodeType => "AuthReference";
    public string Property { get; }

    public AuthReferenceNode(string property)
    {
        Property = property;
    }
}

/// <summary>
/// Explicit record.field reference (e.g., record.amount).
/// </summary>
public sealed class RecordFieldReferenceNode : ConditionNode
{
    public override string NodeType => "RecordFieldReference";
    public string Field { get; }

    public RecordFieldReferenceNode(string field)
    {
        Field = field;
    }
}

/// <summary>
/// EXISTS table WHERE filters (e.g., EXISTS approvals WHERE request_id == record.id AND status == 'approved').
/// </summary>
public sealed class ExistsNode : ConditionNode
{
    public override string NodeType => "Exists";
    public string Table { get; }
    public List<ComparisonNode> Filters { get; }

    public ExistsNode(string table, List<ComparisonNode> filters)
    {
        Table = table;
        Filters = filters;
    }
}

/// <summary>
/// COUNT table WHERE filters op value (e.g., COUNT attempts WHERE subscription_id == record.id >= 3).
/// </summary>
public sealed class CountNode : ConditionNode
{
    public override string NodeType => "Count";
    public string Table { get; }
    public List<ComparisonNode> Filters { get; }
    public string Operator { get; }
    public int Value { get; }

    public CountNode(string table, List<ComparisonNode> filters, string op, int value)
    {
        Table = table;
        Filters = filters;
        Operator = op;
        Value = value;
    }
}

/// <summary>
/// LOOKUP table.selectField WHERE whereField == whereValue op compareValue
/// (e.g., LOOKUP subscriptions.state WHERE id == record.subscription_id == 'active').
/// </summary>
public sealed class LookupNode : ConditionNode
{
    public override string NodeType => "Lookup";
    public string Table { get; }
    public string SelectField { get; }
    public string WhereField { get; }
    public ConditionNode WhereValue { get; }
    public string Operator { get; }
    public ConditionNode CompareValue { get; }

    public LookupNode(string table, string selectField, string whereField,
        ConditionNode whereValue, string op, ConditionNode compareValue)
    {
        Table = table;
        SelectField = selectField;
        WhereField = whereField;
        WhereValue = whereValue;
        Operator = op;
        CompareValue = compareValue;
    }
}
