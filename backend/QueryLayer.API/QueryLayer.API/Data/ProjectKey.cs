namespace QueryLayer.Api.Models;

public class ProjectKey
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string ApiKeyHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}