using Microsoft.AspNetCore.Mvc;
using QueryLayer.Api.Services.Auth;

namespace QueryLayer.Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("signup")]
    public async Task<IActionResult> Signup([FromBody] AuthRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Email and password are required." });

        if (request.ProjectId == Guid.Empty)
            return BadRequest(new { error = "Project ID is required." });

        var result = await _authService.SignupAsync(request.ProjectId, request.Email, request.Password);

        if (!result.Success)
            return Conflict(new { error = result.Error });

        return Ok(new
        {
            token = result.Token,
            userId = result.UserId,
            role = result.Role
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] AuthRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Email and password are required." });

        if (request.ProjectId == Guid.Empty)
            return BadRequest(new { error = "Project ID is required." });

        var result = await _authService.LoginAsync(request.ProjectId, request.Email, request.Password);

        if (!result.Success)
            return Unauthorized(new { error = result.Error });

        return Ok(new
        {
            token = result.Token,
            userId = result.UserId,
            role = result.Role
        });
    }
}

public class AuthRequest
{
    public Guid ProjectId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
