namespace QueryLayer.Api.Models.Runtime;

public class BackendSpec
{
    public List<EntitySpec> Entities { get; set; } = new();
    public List<EndpointSpec> Endpoints { get; set; } = new();
    public List<PermissionSpec> Permissions { get; set; } = new();
}

public class EntitySpec
{
    public string Name { get; set; } = string.Empty;
    public string Table { get; set; } = string.Empty;
    public List<FieldSpec> Fields { get; set; } = new();
}

public class FieldSpec
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "string";
    public bool Primary { get; set; }
    public bool Required { get; set; }
    public bool Unique { get; set; }
    public RelationSpec? Relation { get; set; }
}

public class RelationSpec
{
    public string Table { get; set; } = string.Empty;
    public string Column { get; set; } = "id";
}

public class EndpointSpec
{
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string Entity { get; set; } = string.Empty;
    public string? Auth { get; set; }
}

public class PermissionSpec
{
    public string Entity { get; set; } = string.Empty;
    public List<string> Operations { get; set; } = new();
    public string? Filter { get; set; }
}
