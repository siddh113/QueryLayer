using QueryLayer.Api.Models.Runtime;

namespace QueryLayer.Api.Services.Auth;

public class PermissionService
{
    private readonly ILogger<PermissionService> _logger;

    public PermissionService(ILogger<PermissionService> logger)
    {
        _logger = logger;
    }

    public PermissionResult Evaluate(
        EndpointSpec endpoint,
        EntitySpec entity,
        BackendSpec spec,
        AuthContext authContext)
    {
        var authMode = endpoint.Auth?.ToLowerInvariant() ?? "public";

        // Public endpoints need no auth
        if (authMode == "public")
            return PermissionResult.Allowed();

        // Service endpoints require a secret API key
        if (authMode == "service")
        {
            if (authContext.AuthMethod == "secret_key")
                return PermissionResult.Allowed();

            _logger.LogWarning("Service endpoint {Path} requires a secret API key (x-api-key header).", endpoint.Path);
            return PermissionResult.Denied("Secret API key required for service endpoint", 401);
        }

        // Authenticated/admin endpoints need a valid JWT user
        if (!authContext.IsAuthenticated)
        {
            _logger.LogWarning("Unauthenticated access attempt to {Method} {Path}.", endpoint.Method, endpoint.Path);

            if (authContext.AuthMethod == "public_key")
                return PermissionResult.Denied("Public key cannot be used for authenticated endpoint", 401);

            if (authContext.AuthMethod == "secret_key")
                return PermissionResult.Denied("JWT token required for this endpoint", 401);

            return PermissionResult.Denied("Unauthorized", 401);
        }

        // Admin-only endpoints
        if (authMode == "admin" && authContext.Role != "admin")
        {
            _logger.LogWarning("User {UserId} with role '{Role}' denied access to admin endpoint {Path}.",
                authContext.UserId, authContext.Role, endpoint.Path);
            return PermissionResult.Denied("Forbidden", 403);
        }

        // Admins bypass entity-level permission checks
        if (authContext.Role == "admin")
            return PermissionResult.Allowed();

        // Check entity-level permissions from spec
        var permission = spec.Permissions?.FirstOrDefault(p =>
            p.Entity.Equals(entity.Name, StringComparison.OrdinalIgnoreCase));

        if (permission != null)
        {
            // Check if operation is allowed
            if (permission.Operations != null && permission.Operations.Count > 0)
            {
                if (!permission.Operations.Any(op =>
                    op.Equals(endpoint.Operation, StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogWarning("Operation '{Operation}' not allowed for entity '{Entity}' for user {UserId}.",
                        endpoint.Operation, entity.Name, authContext.UserId);
                    return PermissionResult.Denied("Forbidden", 403);
                }
            }
        }

        return PermissionResult.Allowed();
    }

    public string? GetRowFilter(
        EntitySpec entity,
        BackendSpec spec,
        AuthContext authContext)
    {
        // Admins bypass row-level filters
        if (authContext.Role == "admin")
            return null;

        var permission = spec.Permissions?.FirstOrDefault(p =>
            p.Entity.Equals(entity.Name, StringComparison.OrdinalIgnoreCase));

        if (permission?.Filter == null)
            return null;

        // Replace auth.user_id with actual user ID
        var filter = permission.Filter
            .Replace("auth.user_id", $"'{authContext.UserId}'");

        return filter;
    }
}

public class AuthContext
{
    public bool IsAuthenticated { get; set; }
    public Guid? UserId { get; set; }
    public Guid? ProjectId { get; set; }
    public string Role { get; set; } = "user";
    public string? AuthMethod { get; set; } // "jwt" | "secret_key" | "public_key" | null

    public static AuthContext Anonymous() => new() { IsAuthenticated = false };
}

public class PermissionResult
{
    public bool IsAllowed { get; set; }
    public string? Error { get; set; }
    public int StatusCode { get; set; }

    public static PermissionResult Allowed() => new() { IsAllowed = true };
    public static PermissionResult Denied(string error, int statusCode) =>
        new() { IsAllowed = false, Error = error, StatusCode = statusCode };
}
