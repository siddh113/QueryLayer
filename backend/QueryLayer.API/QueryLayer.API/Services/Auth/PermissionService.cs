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

        // Authenticated/admin endpoints need a valid user
        if (!authContext.IsAuthenticated)
        {
            _logger.LogWarning("Unauthenticated access attempt to {Method} {Path}.", endpoint.Method, endpoint.Path);
            return PermissionResult.Denied("Unauthorized", 401);
        }

        // Admin-only endpoints
        if (authMode == "admin" && authContext.Role != "admin")
        {
            _logger.LogWarning("User {UserId} with role '{Role}' denied access to admin endpoint {Path}.",
                authContext.UserId, authContext.Role, endpoint.Path);
            return PermissionResult.Denied("Forbidden", 403);
        }

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
