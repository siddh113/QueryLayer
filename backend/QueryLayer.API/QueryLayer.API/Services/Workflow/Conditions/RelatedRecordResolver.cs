using QueryLayer.Api.Models.Runtime;

namespace QueryLayer.Api.Services.Workflow.Conditions;

/// <summary>
/// Resolves alias → entity/table mappings using spec relation metadata.
/// For example, if entity PaymentAttempt has field subscription_id with relation { table: "subscriptions", column: "id" },
/// this exposes alias "subscription" → table "subscriptions".
/// </summary>
public sealed class RelatedRecordResolver
{
    /// <summary>
    /// Resolve all available relation aliases for a given entity.
    /// </summary>
    public List<ResolvedRelation> ResolveAll(EntitySpec entity, BackendSpec spec)
    {
        var results = new List<ResolvedRelation>();

        foreach (var field in entity.Fields)
        {
            if (field.Relation == null || string.IsNullOrWhiteSpace(field.Relation.Table))
                continue;

            var relatedEntity = spec.Entities.FirstOrDefault(e =>
                e.Table.Equals(field.Relation.Table, StringComparison.OrdinalIgnoreCase));

            if (relatedEntity == null)
                continue;

            // Derive alias: strip _id suffix from field name if present
            var alias = DeriveAlias(field.Name, field.Relation.Table);

            results.Add(new ResolvedRelation
            {
                Alias = alias,
                ForeignKeyField = field.Name,
                RelatedTable = field.Relation.Table,
                RelatedColumn = field.Relation.Column,
                RelatedEntity = relatedEntity
            });
        }

        return results;
    }

    /// <summary>
    /// Resolve a specific alias to its relation details. Returns null if the alias cannot be resolved.
    /// </summary>
    public ResolvedRelation? Resolve(string alias, EntitySpec entity, BackendSpec spec)
    {
        var all = ResolveAll(entity, spec);
        return all.FirstOrDefault(r =>
            r.Alias.Equals(alias, StringComparison.OrdinalIgnoreCase));
    }

    private static string DeriveAlias(string fieldName, string relatedTable)
    {
        // If field is "subscription_id", alias is "subscription"
        // If field is "order_id", alias is "order"
        if (fieldName.EndsWith("_id", StringComparison.OrdinalIgnoreCase) && fieldName.Length > 3)
            return fieldName[..^3];

        // Otherwise, use the singular form of the table name
        // Simple heuristic: strip trailing 's' if present
        if (relatedTable.EndsWith("s", StringComparison.OrdinalIgnoreCase) && relatedTable.Length > 1)
            return relatedTable[..^1];

        return relatedTable;
    }
}

public sealed class ResolvedRelation
{
    public string Alias { get; set; } = string.Empty;
    public string ForeignKeyField { get; set; } = string.Empty;
    public string RelatedTable { get; set; } = string.Empty;
    public string RelatedColumn { get; set; } = string.Empty;
    public EntitySpec RelatedEntity { get; set; } = new();
}
