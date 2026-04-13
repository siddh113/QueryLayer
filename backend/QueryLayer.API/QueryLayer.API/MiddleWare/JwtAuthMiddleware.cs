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

    public async Task Invoke(HttpContext context, JwtService jwtService, KeyManagementService keyService)
    {
        // 1. Try JWT Bearer token
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        if (authHeader != null && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = authHeader["Bearer ".Length..].Trim();
            var principal = jwtService.ValidateToken(token);

            if (principal != null)
            {
                var userId = principal.FindFirst("user_id")?.Value;
                var projectId = principal.FindFirst("project_id")?.Value;
                var role = principal.FindFirst("role")?.Value
                           ?? principal.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

                if (userId != null) context.Items["user_id"] = userId;
                if (projectId != null) context.Items["project_id"] = projectId;
                if (role != null) context.Items["role"] = role;
                context.Items["auth_method"] = "jwt";

                _logger.LogDebug("JWT authenticated user {UserId} with role {Role}.", userId, role);
            }
            else
            {
                _logger.LogWarning("Invalid JWT token received.");
            }
        }

        // 2. Try secret API key (x-api-key header)
        var apiKey = context.Request.Headers["x-api-key"].FirstOrDefault();
        if (!string.IsNullOrEmpty(apiKey))
        {
            var keyRecord = await keyService.ValidateKeyAsync(apiKey);
            if (keyRecord != null && keyRecord.KeyType == "secret")
            {
                context.Items["api_key_project_id"] = keyRecord.ProjectId.ToString();
                context.Items["api_key_type"] = "secret";
                context.Items["auth_method"] = "secret_key";
                _logger.LogDebug("Secret API key authenticated for project {ProjectId}.", keyRecord.ProjectId);
            }
            else if (keyRecord == null)
            {
                _logger.LogWarning("Invalid or revoked API key presented.");
            }
        }

        // 3. Register public key presence (x-public-key header) — no auth granted
        var publicKey = context.Request.Headers["x-public-key"].FirstOrDefault();
        if (!string.IsNullOrEmpty(publicKey))
        {
            var keyRecord = await keyService.ValidateKeyAsync(publicKey);
            if (keyRecord != null && keyRecord.KeyType == "public")
            {
                context.Items["public_key_project_id"] = keyRecord.ProjectId.ToString();
                context.Items["auth_method"] ??= "public_key";
                _logger.LogDebug("Public key identified project {ProjectId}.", keyRecord.ProjectId);
            }
        }

        await _next(context);
    }
}
