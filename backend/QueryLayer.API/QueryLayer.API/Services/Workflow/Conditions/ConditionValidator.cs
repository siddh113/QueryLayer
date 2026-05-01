using QueryLayer.Api.Models.Runtime;

namespace QueryLayer.Api.Services.Workflow.Conditions;

/// <summary>
/// Validates a parsed condition AST against the spec, ensuring all field references,
/// table references, and operator usage are correct.
/// </summary>
public sealed class ConditionValidator
{
    private static readonly HashSet<string> AllowedAuthProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "role", "user_id"
    };

    private static readonly HashSet<string> NumericTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "integer", "decimal"
    };

    private static readonly HashSet<string> OrderingOperators = new()
    {
        ">", "<", ">=", "<="
    };

    private readonly RelatedRecordResolver _relatedResolver;

    public ConditionValidator(RelatedRecordResolver relatedResolver)
    {
        _relatedResolver = relatedResolver;
    }

    /// <summary>
    /// Validate the given AST node against the spec.
    /// </summary>
    /// <param name="node">Parsed condition AST.</param>
    /// <param name="spec">The full backend spec for table/field resolution.</param>
    /// <param name="entityName">Current entity name (PascalCase).</param>
    /// <param name="eventOperation">Event operation: "created", "updated", or "deleted". Null if validating a workflow transition condition.</param>
    public ConditionValidationResult Validate(
        ConditionNode node,
        BackendSpec spec,
        string entityName,
        string? eventOperation)
    {
        var errors = new List<string>();
        var entity = spec.Entities.FirstOrDefault(e =>
            e.Name.Equals(entityName, StringComparison.OrdinalIgnoreCase));

        if (entity == null)
        {
            errors.Add($"Entity '{entityName}' not found in spec.");
            return new ConditionValidationResult { IsValid = false, Errors = errors };
        }

        var fieldSet = new HashSet<string>(
            entity.Fields.Select(f => f.Name),
            StringComparer.OrdinalIgnoreCase);

        var context = new ValidationContext
        {
            Spec = spec,
            Entity = entity,
            EntityFields = fieldSet,
            EventOperation = eventOperation,
            Errors = errors
        };

        ValidateNode(node, context);

        return new ConditionValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }

    private void ValidateNode(ConditionNode node, ValidationContext ctx)
    {
        switch (node)
        {
            case ComparisonNode comp:
                ValidateNode(comp.Left, ctx);
                ValidateNode(comp.Right, ctx);
                ValidateComparisonOperatorTypes(comp, ctx);
                break;

            case LogicalNode logical:
                ValidateNode(logical.Left, ctx);
                ValidateNode(logical.Right, ctx);
                break;

            case NotNode not:
                ValidateNode(not.Inner, ctx);
                break;

            case FieldReferenceNode fieldRef:
                if (!ctx.EntityFields.Contains(fieldRef.Field))
                    ctx.Errors.Add($"Field '{fieldRef.Field}' does not exist on entity '{ctx.Entity.Name}'.");
                break;

            case RecordFieldReferenceNode recordRef:
                if (!ctx.EntityFields.Contains(recordRef.Field))
                    ctx.Errors.Add($"Field 'record.{recordRef.Field}' does not exist on entity '{ctx.Entity.Name}'.");
                break;

            case PreviousFieldReferenceNode prevRef:
                if (ctx.EventOperation != null &&
                    !ctx.EventOperation.Equals("updated", StringComparison.OrdinalIgnoreCase))
                {
                    ctx.Errors.Add(
                        $"'previous.{prevRef.Field}' can only be used in 'updated' events, not '{ctx.EventOperation}'.");
                }
                if (!ctx.EntityFields.Contains(prevRef.Field))
                    ctx.Errors.Add($"Field 'previous.{prevRef.Field}' does not exist on entity '{ctx.Entity.Name}'.");
                break;

            case AuthReferenceNode authRef:
                if (!AllowedAuthProperties.Contains(authRef.Property))
                    ctx.Errors.Add(
                        $"Auth property 'auth.{authRef.Property}' is not supported. Allowed: {string.Join(", ", AllowedAuthProperties)}.");
                break;

            case RelatedFieldReferenceNode relatedRef:
                ValidateRelatedFieldReference(relatedRef, ctx);
                break;

            case ExistsNode existsNode:
                ValidateTableFilters(existsNode.Table, existsNode.Filters, "EXISTS", ctx);
                break;

            case CountNode countNode:
                ValidateTableFilters(countNode.Table, countNode.Filters, "COUNT", ctx);
                break;

            case LookupNode lookupNode:
                ValidateLookup(lookupNode, ctx);
                break;

            case LiteralNode:
                // Literals are always valid on their own
                break;

            default:
                ctx.Errors.Add($"Unsupported AST node type: {node.GetType().Name}.");
                break;
        }
    }

    private void ValidateRelatedFieldReference(RelatedFieldReferenceNode relatedRef, ValidationContext ctx)
    {
        var resolved = _relatedResolver.Resolve(relatedRef.Alias, ctx.Entity, ctx.Spec);
        if (resolved == null)
        {
            ctx.Errors.Add(
                $"Cannot resolve related alias '{relatedRef.Alias}' on entity '{ctx.Entity.Name}'. " +
                "No matching foreign key relation found.");
            return;
        }

        var relatedFields = new HashSet<string>(
            resolved.RelatedEntity.Fields.Select(f => f.Name),
            StringComparer.OrdinalIgnoreCase);

        if (!relatedFields.Contains(relatedRef.Field))
        {
            ctx.Errors.Add(
                $"Field '{relatedRef.Field}' does not exist on related entity '{resolved.RelatedEntity.Name}' " +
                $"(table '{resolved.RelatedTable}').");
        }
    }

    private void ValidateTableFilters(string table, List<ComparisonNode> filters, string keyword, ValidationContext ctx)
    {
        var entity = ctx.Spec.Entities.FirstOrDefault(e =>
            e.Table.Equals(table, StringComparison.OrdinalIgnoreCase));

        if (entity == null)
        {
            ctx.Errors.Add($"{keyword} references unknown table '{table}'.");
            return;
        }

        var tableFields = new HashSet<string>(
            entity.Fields.Select(f => f.Name),
            StringComparer.OrdinalIgnoreCase);

        foreach (var filter in filters)
        {
            ValidateFilterFieldExists(filter.Left, tableFields, entity.Name, ctx);
            ValidateFilterFieldExists(filter.Right, tableFields, entity.Name, ctx);
        }
    }

    private void ValidateFilterFieldExists(ConditionNode node, HashSet<string> tableFields, string entityName, ValidationContext ctx)
    {
        switch (node)
        {
            case FieldReferenceNode fieldRef:
                if (!tableFields.Contains(fieldRef.Field))
                    ctx.Errors.Add($"Field '{fieldRef.Field}' does not exist on entity '{entityName}'.");
                break;
            case RecordFieldReferenceNode recordRef:
                // record.field references the current entity, not the filter table
                if (!ctx.EntityFields.Contains(recordRef.Field))
                    ctx.Errors.Add($"Field 'record.{recordRef.Field}' does not exist on entity '{ctx.Entity.Name}'.");
                break;
            case AuthReferenceNode authRef:
                if (!AllowedAuthProperties.Contains(authRef.Property))
                    ctx.Errors.Add($"Auth property 'auth.{authRef.Property}' is not supported.");
                break;
            case LiteralNode:
                break;
            default:
                // Other node types in filter context are acceptable (they were validated above)
                break;
        }
    }

    private void ValidateLookup(LookupNode lookupNode, ValidationContext ctx)
    {
        var entity = ctx.Spec.Entities.FirstOrDefault(e =>
            e.Table.Equals(lookupNode.Table, StringComparison.OrdinalIgnoreCase));

        if (entity == null)
        {
            ctx.Errors.Add($"LOOKUP references unknown table '{lookupNode.Table}'.");
            return;
        }

        var tableFields = new HashSet<string>(
            entity.Fields.Select(f => f.Name),
            StringComparer.OrdinalIgnoreCase);

        if (!tableFields.Contains(lookupNode.SelectField))
            ctx.Errors.Add($"LOOKUP select field '{lookupNode.SelectField}' does not exist on table '{lookupNode.Table}'.");

        if (!tableFields.Contains(lookupNode.WhereField))
            ctx.Errors.Add($"LOOKUP where field '{lookupNode.WhereField}' does not exist on table '{lookupNode.Table}'.");

        ValidateNode(lookupNode.WhereValue, ctx);
        ValidateNode(lookupNode.CompareValue, ctx);
    }

    private void ValidateComparisonOperatorTypes(ComparisonNode comp, ValidationContext ctx)
    {
        if (!OrderingOperators.Contains(comp.Operator))
            return;

        // If left is a field reference, check that the field is numeric for ordering operators
        var fieldType = ResolveFieldType(comp.Left, ctx);
        if (fieldType != null && !NumericTypes.Contains(fieldType))
        {
            ctx.Errors.Add(
                $"Ordering operator '{comp.Operator}' cannot be used on field of type '{fieldType}'. " +
                "Only integer and decimal fields support ordering comparisons.");
        }
    }

    private string? ResolveFieldType(ConditionNode node, ValidationContext ctx)
    {
        switch (node)
        {
            case FieldReferenceNode fieldRef:
                return ctx.Entity.Fields
                    .FirstOrDefault(f => f.Name.Equals(fieldRef.Field, StringComparison.OrdinalIgnoreCase))
                    ?.Type;

            case RecordFieldReferenceNode recordRef:
                return ctx.Entity.Fields
                    .FirstOrDefault(f => f.Name.Equals(recordRef.Field, StringComparison.OrdinalIgnoreCase))
                    ?.Type;

            case PreviousFieldReferenceNode prevRef:
                return ctx.Entity.Fields
                    .FirstOrDefault(f => f.Name.Equals(prevRef.Field, StringComparison.OrdinalIgnoreCase))
                    ?.Type;

            default:
                return null;
        }
    }

    private sealed class ValidationContext
    {
        public BackendSpec Spec { get; set; } = new();
        public EntitySpec Entity { get; set; } = new();
        public HashSet<string> EntityFields { get; set; } = new();
        public string? EventOperation { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}

public sealed class ConditionValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
}
