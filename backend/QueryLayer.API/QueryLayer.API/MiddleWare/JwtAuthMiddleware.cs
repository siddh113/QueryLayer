using QueryLayer.Api.Services.Auth;

namespace QueryLayer.Api.Middleware;

public class JwtAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<JwtAuthMiddleware> _logger;

    public JwtAuthMiddleware(RequestDelegate next, ILogger<JwtAuthMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context, JwtService jwtService)
    {
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();

        if (authHeader != null && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = authHeader["Bearer ".Length..].Trim();
            var principal = jwtService.ValidateToken(token);

            if (principal != null)
            {
                var userId = principal.FindFirst("user_id")?.Value;
                var projectId = principal.FindFirst("project_id")?.Value;
                var role = principal.FindFirst("role")?.Value;

                if (userId != null) context.Items["user_id"] = userId;
                if (projectId != null) context.Items["project_id"] = projectId;
                if (role != null) context.Items["role"] = role;

                _logger.LogDebug("JWT authenticated user {UserId} with role {Role}.", userId, role);
            }
            else
            {
                _logger.LogWarning("Invalid JWT token received.");
            }
        }

        await _next(context);
    }
}
