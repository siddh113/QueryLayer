using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QueryLayer.Api.Data;
using QueryLayer.Api.Services.Auth;

namespace QueryLayer.Api.Controllers;

[ApiController]
[Route("projects/{id}/keys")]
public class KeysController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly KeyManagementService _keyService;
    private readonly ILogger<KeysController> _logger;

    public KeysController(AppDbContext db, KeyManagementService keyService, ILogger<KeysController> logger)
    {
        _db = db;
        _keyService = keyService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetKeys(Guid id)
    {
        var project = await _db.Projects.FindAsync(id);
        if (project == null) return NotFound(new { error = "Project not found" });

        var keys = await _keyService.GetKeysAsync(id);
        return Ok(keys.Select(k => new
        {
            k.Id,
            k.KeyType,
            k.KeyPrefix,
            k.Name,
            k.CreatedAt,
            k.LastUsedAt,
            k.RevokedAt,
            IsActive = k.RevokedAt == null
        }));
    }

    [HttpPost("generate")]
    public async Task<IActionResult> GenerateKey(Guid id, [FromBody] GenerateKeyRequest request)
    {
        var project = await _db.Projects.FindAsync(id);
        if (project == null) return NotFound(new { error = "Project not found" });

        var keyType = request.KeyType?.ToLower();
        if (keyType != "public" && keyType != "secret")
            return BadRequest(new { error = "keyType must be 'public' or 'secret'" });

        var (record, rawKey) = await _keyService.GenerateKeyAsync(id, keyType, request.Name);

        return Ok(new
        {
            record.Id,
            record.KeyType,
            record.KeyPrefix,
            record.Name,
            record.CreatedAt,
            RawKey = rawKey,
            Warning = keyType == "secret"
                ? "Save this key now. It will never be shown again."
                : null
        });
    }

    [HttpPost("{keyId}/revoke")]
    public async Task<IActionResult> RevokeKey(Guid id, Guid keyId)
    {
        var project = await _db.Projects.FindAsync(id);
        if (project == null) return NotFound(new { error = "Project not found" });

        var revoked = await _keyService.RevokeKeyAsync(id, keyId);
        if (!revoked) return NotFound(new { error = "Key not found" });

        return Ok(new { message = "Key revoked successfully" });
    }

    [HttpPost("{keyId}/rotate")]
    public async Task<IActionResult> RotateKey(Guid id, Guid keyId)
    {
        var project = await _db.Projects.FindAsync(id);
        if (project == null) return NotFound(new { error = "Project not found" });

        try
        {
            var (record, rawKey) = await _keyService.RotateKeyAsync(id, keyId);
            return Ok(new
            {
                record.Id,
                record.KeyType,
                record.KeyPrefix,
                record.Name,
                record.CreatedAt,
                RawKey = rawKey,
                Warning = "Save this key now. The old key has been revoked and this will not be shown again."
            });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}

public class GenerateKeyRequest
{
    public string KeyType { get; set; } = "secret";
    public string? Name { get; set; }
}
