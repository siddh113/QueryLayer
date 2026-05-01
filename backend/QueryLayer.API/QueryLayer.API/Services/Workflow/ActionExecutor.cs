using System.Text;
using System.Text.Json;
using QueryLayer.Api.Data;
using QueryLayer.Api.Models.Runtime;
using QueryLayer.Api.Services.Runtime;

namespace QueryLayer.Api.Services.Workflow;

public class ExecutionContext
{
    public Dictionary<string, object?> Record { get; set; } = new();
    public Dictionary<string, object?> Auth { get; set; } = new();
    public Guid ProjectId { get; set; }
    public BackendSpec Spec { get; set; } = new();
}

public class ActionExecutor
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SqlQueryBuilder _queryBuilder;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ActionExecutor> _logger;

    public ActionExecutor(
        IHttpClientFactory httpClientFactory,
        SqlQueryBuilder queryBuilder,
        IConfiguration configuration,
        ILogger<ActionExecutor> logger)
    {
        _httpClientFactory = httpClientFactory;
        _queryBuilder = queryBuilder;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task ExecuteAsync(ActionSpec action, ExecutionContext ctx)
    {
        switch (action.Type.ToLowerInvariant())
        {
            case "webhook":
                await ExecuteWebhookAsync(action, ctx);
                break;
            case "create":
                await ExecuteCreateAsync(action, ctx);
                break;
            case "update":
                await ExecuteUpdateAsync(action, ctx);
                break;
            case "delay":
                await ExecuteDelayAsync(action);
                break;
            default:
                _logger.LogWarning("Unknown action type: {Type}", action.Type);
                break;
        }
    }

    private async Task ExecuteWebhookAsync(ActionSpec action, ExecutionContext ctx)
    {
        if (string.IsNullOrWhiteSpace(action.Url))
        {
            _logger.LogWarning("Webhook action missing URL");
            return;
        }

        // Validate URL scheme (no internal/localhost in prod)
        if (!Uri.TryCreate(action.Url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "https" && uri.Scheme != "http"))
        {
            _logger.LogWarning("Invalid webhook URL: {Url}", action.Url);
            return;
        }

        var payload = new
        {
            record = ctx.Record,
            auth = ctx.Auth,
            project_id = ctx.ProjectId
        };

        var json = JsonSerializer.Serialize(payload);
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);

        var response = await client.PostAsync(action.Url,
            new StringContent(json, Encoding.UTF8, "application/json"));

        _logger.LogInformation("Webhook {Url} responded {Status}", action.Url, (int)response.StatusCode);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Webhook returned {(int)response.StatusCode}");
    }

    private async Task ExecuteCreateAsync(ActionSpec action, ExecutionContext ctx)
    {
        if (string.IsNullOrWhiteSpace(action.Entity))
        {
            _logger.LogWarning("Create action missing entity");
            return;
        }

        var entity = ctx.Spec.Entities.FirstOrDefault(e =>
            e.Name.Equals(action.Entity, StringComparison.OrdinalIgnoreCase));
        if (entity == null)
        {
            _logger.LogWarning("Create action: entity {Entity} not found in spec", action.Entity);
            return;
        }

        var data = action.Data ?? new();
        var query = _queryBuilder.BuildCreate(entity, data);
        await ExecuteSqlAsync(query);
        _logger.LogInformation("Created record in {Entity}", action.Entity);
    }

    private async Task ExecuteUpdateAsync(ActionSpec action, ExecutionContext ctx)
    {
        if (string.IsNullOrWhiteSpace(action.Entity) || action.Fields == null)
        {
            _logger.LogWarning("Update action missing entity or fields");
            return;
        }

        var entity = ctx.Spec.Entities.FirstOrDefault(e =>
            e.Name.Equals(action.Entity, StringComparison.OrdinalIgnoreCase));
        if (entity == null)
        {
            _logger.LogWarning("Update action: entity {Entity} not found in spec", action.Entity);
            return;
        }

        // Use record id from context
        if (!ctx.Record.TryGetValue("id", out var idVal) || idVal == null)
        {
            _logger.LogWarning("Update action: no id in record context");
            return;
        }

        var query = _queryBuilder.BuildUpdate(entity, idVal.ToString()!, action.Fields);
        await ExecuteSqlAsync(query);
        _logger.LogInformation("Updated record in {Entity}", action.Entity);
    }

    private static Task ExecuteDelayAsync(ActionSpec _)
    {
        // Delay is handled by JobQueueService via RunAfter; at execution time it's a no-op
        return Task.CompletedTask;
    }

    private async Task ExecuteSqlAsync(SqlQuery query)
    {
        var connStr = Environment.GetEnvironmentVariable("DATABASE_URL") ??
                      _configuration.GetConnectionString("DefaultConnection");
        await using var conn = new Npgsql.NpgsqlConnection(connStr);
        await conn.OpenAsync();
        await using var cmd = new Npgsql.NpgsqlCommand(query.Sql, conn);
        foreach (var (key, value) in query.Parameters)
            cmd.Parameters.AddWithValue(key, value ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }
}
