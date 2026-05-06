using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Threading.RateLimiting;
using QueryLayer.Api.Data;
using QueryLayer.Api.Middleware;
using QueryLayer.Api.Services.AI;
using QueryLayer.Api.Services.Auth;
using QueryLayer.Api.Services.DX;
using QueryLayer.Api.Services.Runtime;
using QueryLayer.Api.Services.Workflow;
using QueryLayer.Api.Services.Workflow.Conditions;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

var databaseUrl =
    Environment.GetEnvironmentVariable("DATABASE_URL") ??
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("DATABASE_URL or DefaultConnection must be set.");

// Render provides postgres:// URIs; convert to Npgsql connection string if needed
static string ResolveConnectionString(string url)
{
    string npgsql;
    if (url.StartsWith("postgres://") || url.StartsWith("postgresql://"))
    {
        var uri = new Uri(url);
        var userInfo = uri.UserInfo.Split(':');
        var user = userInfo[0];
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
        var host = uri.Host;
        var port = uri.Port > 0 ? uri.Port : 5432;
        var db = uri.AbsolutePath.TrimStart('/');
        npgsql = $"Host={host};Port={port};Database={db};Username={user};Password={password}";
    }
    else
    {
        npgsql = url;
    }

    // Ensure SSL settings required for Supabase on Render's Linux containers
    var lower = npgsql.ToLowerInvariant();
    if (!lower.Contains("ssl mode=") && !lower.Contains("sslmode="))
        npgsql += ";SSL Mode=Require";
    if (!lower.Contains("trust server certificate=") && !lower.Contains("trustservercertificate="))
        npgsql += ";Trust Server Certificate=true";
    // Disable connection pooling at the Npgsql level to avoid native SSL pool segfaults on Linux
    if (!lower.Contains("pooling="))
        npgsql += ";Pooling=false";

    // Render free tier has no IPv6; resolve hostname to IPv4 to guarantee reachability.
    // Safe because Trust Server Certificate=true is already set above.
    npgsql = ResolveHostToIPv4(npgsql);

    return npgsql;
}

static string ResolveHostToIPv4(string connectionString)
{
    try
    {
        var parts = connectionString.Split(';');
        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i].Trim();
            if (part.StartsWith("Host=", StringComparison.OrdinalIgnoreCase))
            {
                var host = part.Substring(5);
                if (!System.Net.IPAddress.TryParse(host, out _))
                {
                    var addresses = System.Net.Dns.GetHostAddresses(host);
                    var ipv4 = addresses.FirstOrDefault(a =>
                        a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                    if (ipv4 != null)
                        parts[i] = $"Host={ipv4}";
                }
                break;
            }
        }
        return string.Join(";", parts);
    }
    catch
    {
        return connectionString;
    }
}

var connectionString = ResolveConnectionString(databaseUrl);

// Normalize so all services reading DATABASE_URL directly get the correct format
Environment.SetEnvironmentVariable("DATABASE_URL", connectionString);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());

builder.Services.AddMemoryCache();

builder.Services.AddScoped<SpecService>();
builder.Services.AddScoped<ProjectRuntimeResolver>();
builder.Services.AddSingleton<EndpointMatcher>();
builder.Services.AddSingleton<RequestBodyValidator>();
builder.Services.AddSingleton<SqlQueryBuilder>();
builder.Services.AddScoped<RuntimeExecutor>();

builder.Services.AddSingleton<PostgresTypeMapper>();
builder.Services.AddSingleton<EntityParser>();
builder.Services.AddScoped<SchemaGeneratorService>();
builder.Services.AddScoped<SchemaMigrationService>();
builder.Services.AddScoped<SchemaSyncValidator>();

builder.Services.AddSingleton<PasswordHasher>();
builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<PlatformAuthService>();
builder.Services.AddScoped<PermissionService>();
builder.Services.AddScoped<RbacEvaluator>();
builder.Services.AddScoped<KeyManagementService>();

// AI Services
builder.Services.AddHttpClient<AIService>();
builder.Services.AddSingleton<PromptBuilder>();
builder.Services.AddSingleton<ConditionSyntaxValidator>();
builder.Services.AddSingleton<SpecValidator>();
builder.Services.AddScoped<AIService>();
builder.Services.AddScoped<SpecRepairService>();

// DX Services
builder.Services.AddSingleton<OpenApiGeneratorService>();
builder.Services.AddSingleton<ApiExampleGenerator>();

// Sprint 9: Workflow Condition Engine
builder.Services.AddSingleton<RelatedRecordResolver>();
builder.Services.AddSingleton<ConditionNormalizationService>();
builder.Services.AddSingleton<QueryLayer.Api.Services.Workflow.Conditions.ConditionValidator>();
builder.Services.AddSingleton<ConditionCompiler>();
builder.Services.AddSingleton<ConditionPipeline>();

// Sprint 8: Workflow + Trigger Engine
builder.Services.AddSingleton<ConditionEvaluator>();
builder.Services.AddScoped<JobQueueService>();
builder.Services.AddScoped<TriggerService>();
builder.Services.AddScoped<EventDispatcher>();
builder.Services.AddScoped<ActionExecutor>();
builder.Services.AddScoped<WorkflowEngine>();
builder.Services.AddHostedService<JobWorker>();

// Rate limiting: 100 requests/minute per IP on runtime API routes
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("RuntimePolicy", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 100;
        opt.QueueLimit = 0;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
    options.RejectionStatusCode = 429;
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<JwtAuthMiddleware>();

app.UseRateLimiter();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Ensure auth tables exist
using (var scope = app.Services.CreateScope())
{
    var authService = scope.ServiceProvider.GetRequiredService<AuthService>();
    await authService.EnsureAuthTableAsync();

    var platformAuth = scope.ServiceProvider.GetRequiredService<PlatformAuthService>();
    await platformAuth.EnsurePlatformTablesAsync();

    // Sprint 8: ensure workflow tables exist
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await EnsureWorkflowTablesAsync(db);
}

static async Task EnsureWorkflowTablesAsync(AppDbContext db)
{
    await db.Database.ExecuteSqlRawAsync(@"
        CREATE TABLE IF NOT EXISTS trigger_executions (
            id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            project_id UUID NOT NULL,
            trigger_name TEXT NOT NULL,
            event TEXT NOT NULL,
            condition_result BOOLEAN NOT NULL DEFAULT FALSE,
            status TEXT NOT NULL DEFAULT 'pending',
            error_message TEXT,
            record_json JSONB,
            created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            completed_at TIMESTAMPTZ
        );

        CREATE TABLE IF NOT EXISTS jobs (
            id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            project_id UUID NOT NULL,
            trigger_execution_id UUID NOT NULL,
            action_type TEXT NOT NULL,
            action_json JSONB NOT NULL,
            context_json JSONB NOT NULL,
            status TEXT NOT NULL DEFAULT 'queued',
            attempts INTEGER NOT NULL DEFAULT 0,
            run_after TIMESTAMPTZ,
            created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            completed_at TIMESTAMPTZ,
            error_message TEXT
        );

        CREATE TABLE IF NOT EXISTS job_attempts (
            id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            job_id UUID NOT NULL,
            attempt_number INTEGER NOT NULL,
            status TEXT NOT NULL,
            error_message TEXT,
            attempted_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
        );

        CREATE TABLE IF NOT EXISTS workflow_executions (
            id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            project_id UUID NOT NULL,
            entity TEXT NOT NULL,
            record_id TEXT NOT NULL,
            from_state TEXT NOT NULL,
            to_state TEXT NOT NULL,
            transition_name TEXT NOT NULL,
            user_id UUID,
            user_role TEXT,
            executed_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
        );
    ");
}

app.UseHttpsRedirection();
app.UseCors("FrontendPolicy");
app.UseAuthorization();
app.MapControllers();

await app.RunAsync();
