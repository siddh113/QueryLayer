namespace QueryLayer.Api.Models;

public class ProjectSpec
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string SpecJson { get; set; } = "{}";
    public int Version { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}