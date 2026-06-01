using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TaskFlow.Api.Domain;
using TaskFlow.Api.Infrastructure;
using TaskFlow.Api.Infrastructure.Repositories;

namespace TaskFlow.Api.Features.Auth;

public record RegisterRequest(string Name, string Email, string Password);
public record LoginRequest(string Email, string Password);
public record TokenResponse(string Token, string UserId, string Email, string Role, DateTime ExpiresAt);

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/auth");

        group.MapPost("/register", async (
            [FromBody] RegisterRequest req,
            IUserRepository repo) =>
        {
            var existing = await repo.GetByEmailAsync(req.Email);
            if (existing is not null)
                return Results.Conflict(new { error = "Email already registered." });

            var user = new User
            {
                Name = req.Name,
                Email = req.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password)
            };
            await repo.CreateAsync(user);
            return Results.Created($"/users/{user.Id}", new { user.Id, user.Name, user.Email, user.Role });
        });

        group.MapPost("/token", async (
            [FromBody] LoginRequest req,
            IUserRepository repo,
            IOptions<JwtSettings> jwtOptions) =>
        {
            var user = await repo.GetByEmailAsync(req.Email);
            if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
                return Results.Unauthorized();

            var token = GenerateToken(user, jwtOptions.Value);
            return Results.Ok(token);
        });
    }

    private static TokenResponse GenerateToken(User user, JwtSettings settings)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(settings.ExpiryMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        return new TokenResponse(
            Token: new JwtSecurityTokenHandler().WriteToken(token),
            UserId: user.Id,
            Email: user.Email,
            Role: user.Role.ToString(),
            ExpiresAt: expires);
    }
}
