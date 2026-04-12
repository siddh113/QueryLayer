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
        await _db.SaveChangesAsync();

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
            _logger.LogError(ex, "Schema migration failed for project {ProjectId}", id);
            return StatusCode(500, new { error = "Schema migration failed", details = ex.Message });
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
