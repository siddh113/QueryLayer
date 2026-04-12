using QueryLayer.Api.Models.Runtime;

namespace QueryLayer.Api.Services.Auth;

public class RbacEvaluator
{
    private readonly PermissionService _permissionService;
    private readonly ILogger<RbacEvaluator> _logger;

    public RbacEvaluator(PermissionService permissionService, ILogger<RbacEvaluator> logger)
    {
        _permissionService = permissionService;
        _logger = logger;
    }

    public RbacResult Evaluate(
        EndpointSpec endpoint,
        EntitySpec entity,
        BackendSpec spec,
        AuthContext authContext)
    {
        var permissionResult = _permissionService.Evaluate(endpoint, entity, spec, authContext);

        if (!permissionResult.IsAllowed)
        {
            return new RbacResult
            {
                IsAllowed = false,
                Error = permissionResult.Error,
                StatusCode = permissionResult.StatusCode
            };
        }

        // Get row-level filter if applicable
        var rowFilter = _permissionService.GetRowFilter(entity, spec, authContext);

        return new RbacResult
        {
            IsAllowed = true,
            RowFilter = rowFilter
        };
    }
}

public class RbacResult
{
    public bool IsAllowed { get; set; }
    public string? Error { get; set; }
    public int StatusCode { get; set; }
    public string? RowFilter { get; set; }
}
