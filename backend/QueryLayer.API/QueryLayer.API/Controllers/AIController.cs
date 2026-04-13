using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QueryLayer.Api.Data;
using QueryLayer.Api.Services.AI;
using System.Text.Json;

namespace QueryLayer.Api.Controllers;

[ApiController]
[Route("projects")]
public class AIController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly SpecRepairService _specRepairService;
    private readonly ILogger<AIController> _logger;

    public AIController(
        AppDbContext db,
        SpecRepairService specRepairService,
        ILogger<AIController> logger)
    {
        _db = db;
        _specRepairService = specRepairService;
        _logger = logger;
    }

    [HttpPost("{id}/generate-spec")]
    public async Task<IActionResult> GenerateSpec(Guid id, [FromBody] GenerateSpecRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
            return BadRequest(new { error = "Prompt is required." });

        var project = await _db.Projects.FindAsync(id);
        if (project == null)
            return NotFound(new { error = "Project not found." });

        try
        {
            var result = await _specRepairService.GenerateWithRetryAsync(request.Prompt);

            if (!result.IsValid)
            {
                _logger.LogWarning("Spec generation failed for project {ProjectId}: {Error}", id, result.Error);
                return UnprocessableEntity(new { error = "Failed to generate a valid spec.", details = result.Error });
            }

            var specJson = JsonSerializer.Serialize(result.Spec, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });

            _logger.LogInformation("Spec generated for project {ProjectId}", id);

            return Ok(new { spec = result.Spec, specJson });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating spec for project {ProjectId}", id);
            return StatusCode(500, new { error = "AI service error.", details = ex.Message });
        }
    }

    [HttpPost("{id}/edit-spec")]
    public async Task<IActionResult> EditSpec(Guid id, [FromBody] EditSpecRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Instruction))
            return BadRequest(new { error = "Instruction is required." });

        var project = await _db.Projects.FindAsync(id);
        if (project == null)
            return NotFound(new { error = "Project not found." });

        // Fetch current spec
        var currentSpecRecord = await _db.ProjectSpecs
            .Where(ps => ps.ProjectId == id)
            .OrderByDescending(ps => ps.Version)
            .FirstOrDefaultAsync();

        if (currentSpecRecord == null)
            return NotFound(new { error = "No existing spec found. Use generate-spec first." });

        try
        {
            var result = await _specRepairService.EditWithRetryAsync(currentSpecRecord.SpecJson, request.Instruction);

            if (!result.IsValid)
            {
                _logger.LogWarning("Spec edit failed for project {ProjectId}: {Error}", id, result.Error);
                return UnprocessableEntity(new { error = "Failed to edit spec.", details = result.Error });
            }

            var specJson = JsonSerializer.Serialize(result.Spec, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });

            _logger.LogInformation("Spec edited for project {ProjectId}", id);

            return Ok(new { spec = result.Spec, specJson });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error editing spec for project {ProjectId}", id);
            return StatusCode(500, new { error = "AI service error.", details = ex.Message });
        }
    }
}

public class GenerateSpecRequest
{
    public string Prompt { get; set; } = string.Empty;
}

public class EditSpecRequest
{
    public string Instruction { get; set; } = string.Empty;
}
