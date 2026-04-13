namespace QueryLayer.Api.Services.AI;

public class SpecRepairService
{
    private readonly AIService _aiService;
    private readonly SpecValidator _specValidator;
    private readonly ILogger<SpecRepairService> _logger;
    private const int MaxRetries = 2;

    public SpecRepairService(AIService aiService, SpecValidator specValidator, ILogger<SpecRepairService> logger)
    {
        _aiService = aiService;
        _specValidator = specValidator;
        _logger = logger;
    }

    public async Task<SpecValidationResult> GenerateWithRetryAsync(string userPrompt)
    {
        // Attempt 1: default model
        var json = await _aiService.GenerateSpecAsync(userPrompt);
        var result = _specValidator.Validate(json);

        if (result.IsValid)
            return result;

        _logger.LogWarning("Initial generation failed validation: {Error}", result.Error);

        // Attempt 2: repair with default model
        json = await _aiService.RepairSpecAsync(json, result.Error!);
        result = _specValidator.Validate(json);

        if (result.IsValid)
            return result;

        _logger.LogWarning("Repair attempt failed: {Error}. Escalating to stronger model.", result.Error);

        // Attempt 3: stronger model
        json = await _aiService.GenerateSpecAsync(userPrompt, useStrongerModel: true);
        result = _specValidator.Validate(json);

        if (result.IsValid)
            return result;

        _logger.LogError("All generation attempts failed. Last error: {Error}", result.Error);
        return result;
    }

    public async Task<SpecValidationResult> EditWithRetryAsync(string currentSpec, string instruction)
    {
        // Attempt 1: default model
        var json = await _aiService.EditSpecAsync(currentSpec, instruction);
        var result = _specValidator.Validate(json);

        if (result.IsValid)
            return result;

        _logger.LogWarning("Initial edit failed validation: {Error}", result.Error);

        // Attempt 2: repair
        json = await _aiService.RepairSpecAsync(json, result.Error!);
        result = _specValidator.Validate(json);

        if (result.IsValid)
            return result;

        _logger.LogWarning("Repair attempt failed: {Error}. Escalating to stronger model.", result.Error);

        // Attempt 3: stronger model
        json = await _aiService.EditSpecAsync(currentSpec, instruction, useStrongerModel: true);
        result = _specValidator.Validate(json);

        if (result.IsValid)
            return result;

        _logger.LogError("All edit attempts failed. Last error: {Error}", result.Error);
        return result;
    }
}
