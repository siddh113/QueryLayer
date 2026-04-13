using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using QueryLayer.Api.Data;
using QueryLayer.Api.Models.Runtime;

namespace QueryLayer.Api.Services.Runtime;

public class ProjectRuntimeResolver
{
    private readonly AppDbContext _db;
    private readonly SpecService _specService;

    public ProjectRuntimeResolver(AppDbContext db, SpecService specService)
    {
        _db = db;
        _specService = specService;
    }

    public async Task<BackendSpec?> ResolveSpecAsync(string apiKey)
    {
        // Try resolving as a direct project UUID (used by the dashboard explorer)
        if (Guid.TryParse(apiKey, out var projectId))
        {
            var exists = await _db.Projects.AnyAsync(p => p.Id == projectId);
            if (exists)
                return await _specService.GetSpecAsync(projectId);
        }

        // Fall back to hashed API key lookup
        var hash = HashApiKey(apiKey);
        var projectKey = await _db.ProjectKeys
            .FirstOrDefaultAsync(pk => pk.ApiKeyHash == hash);

        if (projectKey == null) return null;

        return await _specService.GetSpecAsync(projectKey.ProjectId);
    }

    private static string HashApiKey(string apiKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
