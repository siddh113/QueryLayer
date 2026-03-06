namespace QueryLayer.Api.Models;

public class Project
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? OwnerUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}