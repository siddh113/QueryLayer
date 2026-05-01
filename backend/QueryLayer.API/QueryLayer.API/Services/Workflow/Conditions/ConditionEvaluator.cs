using Microsoft.Extensions.Logging;
using QueryLayer.Api.Models.Runtime;

namespace QueryLayer.Api.Services.Workflow.Conditions;

/// <summary>
/// Full condition evaluation pipeline: normalize → parse → validate → compile.
/// This is the primary entry point for evaluating conditions in triggers and workflows.
/// </summary>
public sealed class ConditionPipeline
{
    private readonly ConditionNormalizationService _normalizer;
    private readonly ConditionValidator _validator;
    private readonly ConditionCompiler _compiler;
    private readonly RelatedRecordResolver _relatedResolver;
    private readonly ILogger<ConditionPipeline> _logger;

    public ConditionPipeline(
        ConditionNormalizationService normalizer,
        ConditionValidator validator,
        ConditionCompiler compiler,
        RelatedRecordResolver relatedResolver,
        ILogger<ConditionPipeline> logger)
    {
        _normalizer = normalizer;
        _validator = validator;
        _compiler = compiler;
        _relatedResolver = relatedResolver;
        _logger = logger;
    }

    /// <summary>
    /// Evaluate a condition string through the full pipeline: normalize, parse, validate, compile.
    /// </summary>
    public ConditionEvalResult Evaluate(string condition, ConditionContext context)
    {
        if (string.IsNullOrWhiteSpace(condition))
        {
            return new ConditionEvalResult
            {
                Matched = true,
                SqlFragment = "TRUE",
                Parameters = new Dictionary<string, object>()
            };
        }

        var errors = new List<string>();

        // Step 1: Check for injection patterns
        var threats = SafeSqlExpressionBuilder.DetectInjectionPatterns(condition);
        if (threats.Count > 0)
        {
            _logger.LogWarning("Condition rejected due to injection patterns: {Condition}", condition);
            return new ConditionEvalResult
            {
                Matched = false,
                Errors = threats
            };
        }

        // Step 2: Resolve current entity
        var entity = context.Spec.Entities.FirstOrDefault(e =>
            e.Name.Equals(context.Entity, StringComparison.OrdinalIgnoreCase));

        if (entity == null)
        {
            errors.Add($"Entity '{context.Entity}' not found in spec.");
            return new ConditionEvalResult { Matched = false, Errors = errors };
        }

        // Step 3: Normalize SQL subqueries to DSL syntax
        var normResult = _normalizer.Normalize(condition, context.Spec, entity);
        var normalizedCondition = normResult.NormalizedCondition;

        if (normResult.Warnings.Count > 0)
        {
            _logger.LogWarning(
                "Condition normalization warnings for '{Condition}': {Warnings}",
                condition, string.Join("; ", normResult.Warnings));
        }

        // Step 4: Parse
        ConditionNode ast;
        try
        {
            ast = ConditionParser.Parse(normalizedCondition);
        }
        catch (ConditionParseException ex)
        {
            errors.Add($"Parse error: {ex.Message}");
            return new ConditionEvalResult { Matched = false, Errors = errors };
        }

        // Step 5: Determine event operation from context
        string? eventOperation = null;
        if (!string.IsNullOrWhiteSpace(context.Event))
        {
            var parts = context.Event.Split('.');
            if (parts.Length == 2)
                eventOperation = parts[1];
        }

        // Step 6: Validate
        var validationResult = _validator.Validate(ast, context.Spec, context.Entity, eventOperation);
        if (!validationResult.IsValid)
        {
            return new ConditionEvalResult
            {
                Matched = false,
                Errors = validationResult.Errors
            };
        }

        // Step 7: Compile to SQL
        CompileResult compiled;
        try
        {
            compiled = _compiler.Compile(ast, context.Table, context.Spec, entity);
        }
        catch (ConditionCompileException ex)
        {
            errors.Add($"Compile error: {ex.Message}");
            return new ConditionEvalResult { Matched = false, Errors = errors };
        }

        // Step 8: Inject context parameters (previous.*, auth.*)
        InjectContextParameters(compiled.Parameters, context);

        return new ConditionEvalResult
        {
            Matched = true,
            SqlFragment = compiled.SqlFragment,
            Parameters = compiled.Parameters
        };
    }

    private static void InjectContextParameters(Dictionary<string, object> parameters, ConditionContext context)
    {
        // Inject previous record values
        if (context.Previous != null)
        {
            foreach (var (key, value) in context.Previous)
            {
                var paramName = $"prev_{key}";
                if (parameters.ContainsKey(paramName) && value != null)
                    parameters[paramName] = value;
            }
        }

        // Inject auth context
        if (context.Auth != null)
        {
            if (context.Auth.TryGetValue("role", out var role) && parameters.ContainsKey("auth_role"))
                parameters["auth_role"] = role;

            if (context.Auth.TryGetValue("user_id", out var userId) && parameters.ContainsKey("auth_user_id"))
                parameters["auth_user_id"] = userId;
        }
    }
}

/// <summary>
/// Context for condition evaluation, carrying the full execution environment.
/// </summary>
public sealed class ConditionContext
{
    public string ProjectId { get; set; } = string.Empty;
    public string Entity { get; set; } = string.Empty;
    public string Table { get; set; } = string.Empty;
    public string Event { get; set; } = string.Empty;
    public Dictionary<string, object> Record { get; set; } = new();
    public Dictionary<string, object>? Previous { get; set; }
    public Dictionary<string, string>? Auth { get; set; }
    public BackendSpec Spec { get; set; } = new();
}

/// <summary>
/// Result of the full condition evaluation pipeline.
/// </summary>
public sealed class ConditionEvalResult
{
    public bool Matched { get; set; }
    public string SqlFragment { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}
