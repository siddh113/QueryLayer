namespace QueryLayer.Api.Models.Runtime;

public class BackendSpec
{
    public List<EntitySpec> Entities { get; set; } = new();
    public List<EndpointSpec> Endpoints { get; set; } = new();
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
}

public class EndpointSpec
{
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string Entity { get; set; } = string.Empty;
}
