using Microsoft.AspNetCore.Mvc;
using QueryLayer.Api.Data;
using QueryLayer.Api.Services.DX;
using QueryLayer.Api.Services.Runtime;

namespace QueryLayer.Api.Controllers;

[ApiController]
[Route("projects")]
public class DxController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly SpecService _specService;
    private readonly EntityParser _entityParser;
    private readonly OpenApiGeneratorService _openApiGenerator;
    private readonly ApiExampleGenerator _exampleGenerator;
    private readonly ILogger<DxController> _logger;

    public DxController(
        AppDbContext db,
        SpecService specService,
        EntityParser entityParser,
        OpenApiGeneratorService openApiGenerator,
        ApiExampleGenerator exampleGenerator,
        ILogger<DxController> logger)
    {
        _db = db;
        _specService = specService;
        _entityParser = entityParser;
        _openApiGenerator = openApiGenerator;
        _exampleGenerator = exampleGenerator;
        _logger = logger;
    }

    [HttpGet("{id}/openapi")]
    public async Task<IActionResult> GetOpenApi(Guid id)
    {
        var project = await _db.Projects.FindAsync(id);
        if (project == null)
            return NotFound(new { error = "Project not found" });

        var spec = await _specService.GetSpecAsync(id);
        if (spec == null)
            return NotFound(new { error = "No spec found for project" });

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var openApi = _openApiGenerator.Generate(spec, id.ToString(), baseUrl);

        _logger.LogInformation("OpenAPI spec generated for project {ProjectId}", id);
        return Ok(openApi);
    }

    [HttpGet("{id}/examples")]
    public async Task<IActionResult> GetExamples(Guid id)
    {
        var project = await _db.Projects.FindAsync(id);
        if (project == null)
            return NotFound(new { error = "Project not found" });

        var spec = await _specService.GetSpecAsync(id);
        if (spec == null)
            return NotFound(new { error = "No spec found for project" });

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var examples = _exampleGenerator.GenerateAll(spec, id.ToString(), baseUrl);

        return Ok(examples);
    }
}
