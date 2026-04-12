using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace QueryLayer.Api.Services.Auth;

public class JwtService
{
    private readonly string _secret;
    private readonly ILogger<JwtService> _logger;

    public JwtService(IConfiguration configuration, ILogger<JwtService> logger)
    {
        _secret = Environment.GetEnvironmentVariable("JWT_SECRET")
                  ?? configuration["Jwt:Secret"]
                  ?? throw new InvalidOperationException("JWT_SECRET is not configured.");
        _logger = logger;
    }

    public string GenerateToken(Guid userId, Guid projectId, string role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("user_id", userId.ToString()),
            new Claim("project_id", projectId.ToString()),
            new Claim("role", role)
        };

        var token = new JwtSecurityToken(
            issuer: "QueryLayer",
            audience: "QueryLayer",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GeneratePlatformToken(Guid userId, string role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("user_id", userId.ToString()),
            new Claim("role", role),
            new Claim("scope", "platform")
        };

        var token = new JwtSecurityToken(
            issuer: "QueryLayer",
            audience: "QueryLayer",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var handler = new JwtSecurityTokenHandler();

        try
        {
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = "QueryLayer",
                ValidateAudience = true,
                ValidAudience = "QueryLayer",
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1)
            }, out _);

            return principal;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "JWT validation failed.");
            return null;
        }
    }
}
