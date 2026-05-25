using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QueryLayer.Api.Data;
using QueryLayer.Api.Models;
using QueryLayer.Api.Services.Runtime;
using System.Text.Json;

namespace QueryLayer.Api.Controllers;

[ApiController]
[Route("projects")]
public class SchemaController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly EntityParser _entityParser;
    private readonly SchemaGeneratorService _schemaGenerator;
    private readonly SchemaMigrationService _migrationService;
    private readonly SchemaSyncValidator _syncValidator;
    private readonly SpecService _specService;
    private readonly ILogger<SchemaController> _logger;

    public SchemaController(
        AppDbContext db,
        EntityParser entityParser,
        SchemaGeneratorService schemaGenerator,
        SchemaMigrationService migrationService,
        SchemaSyncValidator syncValidator,
        SpecService specService,
        ILogger<SchemaController> logger)
    {
        _db = db;
        _entityParser = entityParser;
        _schemaGenerator = schemaGenerator;
        _migrationService = migrationService;
        _syncValidator = syncValidator;
        _specService = specService;
        _logger = logger;
    }

    [HttpPut("{id}/spec")]
    public async Task<IActionResult> SaveSpec(Guid id, [FromBody] JsonElement specJson)
    {
        var project = await _db.Projects.FindAsync(id);
        if (project == null)
            return NotFound(new { error = "Project not found" });

        var specString = specJson.GetRawText();

        // 1. Validate spec
        List<QueryLayer.Api.Models.Runtime.EntitySpec> entities;
        try
        {
            var spec = _entityParser.Parse(specString);
            entities = spec.Entities;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Spec validation failed for project {ProjectId}", id);
            return BadRequest(new { error = "Invalid spec", details = ex.Message });
        }

        // 2. Save spec to database
        var latestVersion = await _db.ProjectSpecs
            .Where(ps => ps.ProjectId == id)
            .MaxAsync(ps => (int?)ps.Version) ?? 0;

        var projectSpec = new ProjectSpec
        {
            Id = Guid.NewGuid(),
            ProjectId = id,
            SpecJson = specString,
            Version = latestVersion + 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.ProjectSpecs.Add(projectSpec);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            var innerMsg = ex.InnerException?.InnerException?.Message
                           ?? ex.InnerException?.Message
                           ?? ex.Message;
            _logger.LogError(ex, "Failed to save spec to database for project {ProjectId}. Inner: {Inner}", id, innerMsg);
            return StatusCode(500, new { error = "Failed to save spec to database", details = innerMsg });
        }

        // 3. Generate schema SQL and 4. Execute migrations
        try
        {
            var syncStatements = await _syncValidator.GenerateSyncStatements(entities);

            if (syncStatements.Count > 0)
            {
                await _migrationService.ExecuteAsync(syncStatements);
                _logger.LogInformation("Schema migration completed for project {ProjectId}. {Count} statements executed.",
                    id, syncStatements.Count);
            }
            else
            {
                _logger.LogInformation("Schema already in sync for project {ProjectId}.", id);
            }
        }
        catch (Exception ex)
        {
            var innerMsg = ex.InnerException?.InnerException?.Message
                           ?? ex.InnerException?.Message
                           ?? ex.Message;
            _logger.LogError(ex, "Schema migration failed for project {ProjectId}. Root cause: {Inner}", id, innerMsg);
            return StatusCode(500, new { error = "Schema migration failed", details = innerMsg });
        }

        // 5. Refresh runtime cache
        _specService.InvalidateCache(id);

        return Ok(new
        {
            message = "Spec saved and schema synchronized",
            version = projectSpec.Version,
            projectId = id
        });
    }

    [HttpGet("{id}/schema/tables")]
    public async Task<IActionResult> GetLiveSchema(Guid id)
    {
        var project = await _db.Projects.FindAsync(id);
        if (project == null)
            return NotFound(new { error = "Project not found" });

        var projectSpec = await _db.ProjectSpecs
            .Where(ps => ps.ProjectId == id)
            .OrderByDescending(ps => ps.Version)
            .FirstOrDefaultAsync();

        if (projectSpec == null)
            return Ok(new { tables = Array.Empty<object>() });

        List<QueryLayer.Api.Models.Runtime.EntitySpec> entities;
        try
        {
            var spec = _entityParser.Parse(projectSpec.SpecJson);
            entities = spec.Entities;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse spec for live schema for project {ProjectId}", id);
            return Ok(new { tables = Array.Empty<object>() });
        }

        var tables = new List<object>();
        foreach (var entity in entities)
        {
            var tableName = entity.Table.ToLowerInvariant();
            var exists = await _migrationService.TableExistsAsync(tableName);
            var columns = exists
                ? await _migrationService.GetExistingColumnsAsync(tableName)
                : new List<TableColumnInfo>();

            tables.Add(new
            {
                name = tableName,
                entityName = entity.Name,
                exists,
                columns = columns.Select(c => new
                {
                    columnName = c.ColumnName,
                    dataType = c.DataType,
                    isNullable = c.IsNullable,
                    maxLength = c.MaxLength
                })
            });
        }

        return Ok(new { tables });
    }

    [HttpPost("{id}/spec/preview")]
    public async Task<IActionResult> PreviewSpec(Guid id, [FromBody] JsonElement specJson)
    {
        var project = await _db.Projects.FindAsync(id);
        if (project == null)
            return NotFound(new { error = "Project not found" });

        var specString = specJson.GetRawText();

        List<QueryLayer.Api.Models.Runtime.EntitySpec> entities;
        try
        {
            var spec = _entityParser.Parse(specString);
            entities = spec.Entities;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Spec parse failed during preview for project {ProjectId}", id);
            return BadRequest(new { error = "Invalid spec", details = ex.Message });
        }

        try
        {
            var syncResult = await _syncValidator.ValidateAsync(entities);
            var migrationSql = await _syncValidator.GenerateSyncStatements(entities);

            return Ok(new
            {
                entities,
                syncResult,
                migrationSql,
                hasChanges = migrationSql.Count > 0
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Spec preview failed for project {ProjectId}", id);
            return StatusCode(500, new { error = "Preview failed", details = ex.Message });
        }
    }

    [HttpGet("{id}/schema/validate")]
    public async Task<IActionResult> ValidateSchema(Guid id)
    {
        var project = await _db.Projects.FindAsync(id);
        if (project == null)
            return NotFound(new { error = "Project not found" });

        var projectSpec = await _db.ProjectSpecs
            .Where(ps => ps.ProjectId == id)
            .OrderByDescending(ps => ps.Version)
            .FirstOrDefaultAsync();

        if (projectSpec == null)
            return NotFound(new { error = "No spec found for project" });

        try
        {
            var spec = _entityParser.Parse(projectSpec.SpecJson);
            var result = await _syncValidator.ValidateAsync(spec.Entities);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Schema validation failed for project {ProjectId}", id);
            return StatusCode(500, new { error = "Schema validation failed", details = ex.Message });
        }
    }
}
