using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using QueryLayer.Api.Data;
using QueryLayer.Api.Models.Runtime;

namespace QueryLayer.Api.Services.Runtime;

public class SpecService
{
    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public SpecService(AppDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<BackendSpec?> GetSpecAsync(Guid projectId)
    {
        var cacheKey = $"spec:{projectId}";
        if (_cache.TryGetValue(cacheKey, out BackendSpec? cached))
            return cached;

        var projectSpec = await _db.ProjectSpecs
            .Where(ps => ps.ProjectId == projectId)
            .OrderByDescending(ps => ps.Version)
            .FirstOrDefaultAsync();

        if (projectSpec == null) return null;

        var spec = JsonSerializer.Deserialize<BackendSpec>(projectSpec.SpecJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (spec != null)
            _cache.Set(cacheKey, spec, CacheDuration);

        return spec;
    }

    public void InvalidateCache(Guid projectId)
    {
        _cache.Remove($"spec:{projectId}");
    }
}
