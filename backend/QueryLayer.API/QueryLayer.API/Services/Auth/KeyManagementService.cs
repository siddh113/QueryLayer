using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using QueryLayer.Api.Data;

namespace QueryLayer.Api.Services.Auth;

public class KeyManagementService
{
    private readonly AppDbContext _db;
    private readonly ILogger<KeyManagementService> _logger;

    public KeyManagementService(AppDbContext db, ILogger<KeyManagementService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<(ProjectApiKey record, string rawKey)> GenerateKeyAsync(
        Guid projectId, string keyType, string? name = null)
    {
        var prefix = keyType == "secret" ? "ql_sec" : "ql_pub";
        var rawRandom = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "").Replace("/", "").Replace("=", "")[..32];
        var rawKey = $"{prefix}_{rawRandom}";
        var keyPrefix = rawKey[..Math.Min(16, rawKey.Length)];
        var keyHash = HashKey(rawKey);

        var record = new ProjectApiKey
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            KeyType = keyType,
            KeyHash = keyHash,
            KeyPrefix = keyPrefix,
            Name = name,
            CreatedAt = DateTime.UtcNow
        };

        _db.ProjectApiKeys.Add(record);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Generated {KeyType} key {KeyPrefix}*** for project {ProjectId}.",
            keyType, keyPrefix, projectId);

        return (record, rawKey);
    }

    public async Task<ProjectApiKey?> ValidateKeyAsync(string rawKey)
    {
        var hash = HashKey(rawKey);
        var key = await _db.ProjectApiKeys
            .FirstOrDefaultAsync(k => k.KeyHash == hash && k.RevokedAt == null);

        if (key == null)
        {
            _logger.LogWarning("API key validation failed: key not found or revoked.");
            return null;
        }

        key.LastUsedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return key;
    }

    public async Task<List<ProjectApiKey>> GetKeysAsync(Guid projectId)
    {
        return await _db.ProjectApiKeys
            .Where(k => k.ProjectId == projectId)
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> RevokeKeyAsync(Guid projectId, Guid keyId)
    {
        var key = await _db.ProjectApiKeys
            .FirstOrDefaultAsync(k => k.Id == keyId && k.ProjectId == projectId);

        if (key == null) return false;

        key.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Revoked key {KeyId} for project {ProjectId}.", keyId, projectId);
        return true;
    }

    public async Task<(ProjectApiKey record, string rawKey)> RotateKeyAsync(
        Guid projectId, Guid keyId)
    {
        var old = await _db.ProjectApiKeys
            .FirstOrDefaultAsync(k => k.Id == keyId && k.ProjectId == projectId);

        if (old == null)
            throw new InvalidOperationException("Key not found.");

        old.RevokedAt = DateTime.UtcNow;

        var (record, rawKey) = await GenerateKeyAsync(projectId, old.KeyType, old.Name);
        _logger.LogInformation("Rotated key {OldKeyId} → {NewKeyId} for project {ProjectId}.",
            keyId, record.Id, projectId);

        return (record, rawKey);
    }

    private static string HashKey(string rawKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
