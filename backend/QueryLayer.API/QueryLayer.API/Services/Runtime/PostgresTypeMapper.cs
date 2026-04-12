namespace QueryLayer.Api.Services.Runtime;

public class PostgresTypeMapper
{
    private static readonly Dictionary<string, string> TypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["string"] = "varchar(255)",
        ["integer"] = "int",
        ["boolean"] = "boolean",
        ["uuid"] = "uuid",
        ["timestamp"] = "timestamptz",
        ["text"] = "text"
    };

    public string Map(string specType)
    {
        if (TypeMap.TryGetValue(specType, out var pgType))
            return pgType;

        throw new InvalidOperationException($"Unsupported field type: '{specType}'");
    }

    public bool IsSupported(string specType) =>
        TypeMap.ContainsKey(specType);
}
