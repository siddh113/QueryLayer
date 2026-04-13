using System.Text;
using System.Text.Json;

namespace QueryLayer.Api.Services.AI;

public class AIService
{
    private readonly HttpClient _httpClient;
    private readonly PromptBuilder _promptBuilder;
    private readonly ILogger<AIService> _logger;
    private readonly string _apiKey;
    private const string DefaultModel = "gpt-4.1-mini";
    private const string StrongerModel = "gpt-4.1";

    public AIService(HttpClient httpClient, PromptBuilder promptBuilder, ILogger<AIService> logger)
    {
        _httpClient = httpClient;
        _promptBuilder = promptBuilder;
        _logger = logger;
        _apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            ?? throw new InvalidOperationException("OPENAI_API_KEY environment variable is not set.");
    }

    public async Task<string> GenerateSpecAsync(string userPrompt, bool useStrongerModel = false)
    {
        var model = useStrongerModel ? StrongerModel : DefaultModel;
        var systemPrompt = _promptBuilder.GetSystemPrompt();
        var userMessage = _promptBuilder.BuildGeneratePrompt(userPrompt);

        _logger.LogInformation("Generating spec with model {Model}. Prompt length: {Length}", model, userPrompt.Length);

        return await CallOpenAIAsync(systemPrompt, userMessage, model);
    }

    public async Task<string> EditSpecAsync(string currentSpec, string instruction, bool useStrongerModel = false)
    {
        var model = useStrongerModel ? StrongerModel : DefaultModel;
        var systemPrompt = _promptBuilder.GetSystemPrompt();
        var userMessage = _promptBuilder.BuildEditPrompt(currentSpec, instruction);

        _logger.LogInformation("Editing spec with model {Model}. Instruction length: {Length}", model, instruction.Length);

        return await CallOpenAIAsync(systemPrompt, userMessage, model);
    }

    public async Task<string> RepairSpecAsync(string invalidJson, string validationErrors)
    {
        var systemPrompt = _promptBuilder.GetSystemPrompt();
        var userMessage = $"The following JSON spec is invalid:\n\n{invalidJson}\n\nValidation errors:\n{validationErrors}\n\nFix the spec and return only valid JSON.";

        _logger.LogInformation("Repairing spec. Errors: {Errors}", validationErrors);

        return await CallOpenAIAsync(systemPrompt, userMessage, DefaultModel);
    }

    private async Task<string> CallOpenAIAsync(string systemPrompt, string userMessage, string model)
    {
        var requestBody = new
        {
            model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userMessage }
            },
            temperature = 0.2
        };

        var json = JsonSerializer.Serialize(requestBody);
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Authorization", $"Bearer {_apiKey}");

        var response = await _httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("OpenAI API error: {Status} {Body}", response.StatusCode, responseBody.Length > 500 ? responseBody[..500] : responseBody);
            throw new InvalidOperationException($"OpenAI API returned {response.StatusCode}");
        }

        using var doc = JsonDocument.Parse(responseBody);
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("OpenAI returned empty content.");

        _logger.LogInformation("AI response received. Length: {Length}", content.Length);

        return CleanJsonResponse(content);
    }

    private static string CleanJsonResponse(string response)
    {
        var trimmed = response.Trim();

        // Strip markdown code fences if present
        if (trimmed.StartsWith("```"))
        {
            var firstNewline = trimmed.IndexOf('\n');
            if (firstNewline >= 0)
                trimmed = trimmed[(firstNewline + 1)..];

            if (trimmed.EndsWith("```"))
                trimmed = trimmed[..^3];

            trimmed = trimmed.Trim();
        }

        return trimmed;
    }
}
