namespace QueryLayer.Api.Data;

public class ProjectApiKey
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string KeyType { get; set; } = string.Empty;   // "public" | "secret"
    public string KeyHash { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty; // e.g. "ql_sec_abc123"
    public string? Name { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}
