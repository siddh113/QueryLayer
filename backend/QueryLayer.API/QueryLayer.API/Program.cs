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

    return npgsql;
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
builder.Services.AddSingleton<SpecValidator>();
builder.Services.AddScoped<AIService>();
builder.Services.AddScoped<SpecRepairService>();

// DX Services
builder.Services.AddSingleton<OpenApiGeneratorService>();
builder.Services.AddSingleton<ApiExampleGenerator>();

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
}

app.UseHttpsRedirection();
app.UseCors("FrontendPolicy");
app.UseAuthorization();
app.MapControllers();

await app.RunAsync();
