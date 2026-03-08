using Microsoft.EntityFrameworkCore;
using Serilog;
using QueryLayer.Api.Data;
using QueryLayer.Api.Middleware;
using QueryLayer.Api.Services.Runtime;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

var databaseUrl =
    Environment.GetEnvironmentVariable("DATABASE_URL") ??
    builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(databaseUrl).UseSnakeCaseNamingConvention());

builder.Services.AddMemoryCache();

builder.Services.AddScoped<SpecService>();
builder.Services.AddScoped<ProjectRuntimeResolver>();
builder.Services.AddSingleton<EndpointMatcher>();
builder.Services.AddSingleton<RequestBodyValidator>();
builder.Services.AddSingleton<SqlQueryBuilder>();
builder.Services.AddScoped<RuntimeExecutor>();

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

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("FrontendPolicy");
app.UseAuthorization();
app.MapControllers();

app.Run();
