using Api.Data;
using Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Api.Services;

public interface IAuthService
{
    User Register(UserAuthDto request);
    Task<TokenResponseDto> LoginAsync(UserAuthDto request);
    Task<TokenResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request);
}

public class AuthService(AppDbContext context, IConfiguration configuration) : IAuthService
{
    private readonly PasswordHasher<User> _hasher = new();

    public User Register(UserAuthDto request)
    {
        if (context.Users.Any(u => u.Username == request.Username))
        {
            throw new InvalidOperationException("User already exists.");
        }

        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Username = request.Username
        };

        user.PasswordHash = _hasher.HashPassword(user, request.Password);

        context.Users.Add(user);
        context.SaveChanges();

        return user;
    }

    public async Task<TokenResponseDto> LoginAsync(UserAuthDto request)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.Username == request.Username);

        if (user is null)
        {
            throw new UnauthorizedAccessException("Credentials are not valid!");
        }

        var verifyPassword = _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

        if (verifyPassword == PasswordVerificationResult.Failed)
        {
            throw new UnauthorizedAccessException("Credentials are not valid!");
        }

        var accessToken = GenerateAccessToken(user);
        var refreshToken = GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        await context.SaveChangesAsync();

        return new TokenResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken
        };
    }

    public async Task<TokenResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request)
    {
        var principal = GetPrincipalFromExpiredToken(request.AccessToken);
        if (principal == null)
        {
            throw new SecurityTokenException("Invalid access token or refresh token");
        }

        var username = principal.Identity?.Name;
        if (username == null)
        {
            throw new SecurityTokenException("Invalid access token or refresh token");
        }

        var user = await context.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null || user.RefreshToken != request.RefreshToken || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
        {
            throw new SecurityTokenException("Invalid access token or refresh token");
        }

        var newAccessToken = GenerateAccessToken(user);
        var newRefreshToken = GenerateRefreshToken();

        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        await context.SaveChangesAsync();

        return new TokenResponseDto
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken
        };
    }

    private string GenerateAccessToken(User user)
    {
        var claims = new List<Claim>{
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.Username),
        };

        var tokenKey = configuration.GetValue<string>("Jwt:Key");
        if (string.IsNullOrEmpty(tokenKey))
        {
            throw new InvalidOperationException("Jwt:Key is missing in configuration.");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tokenKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha512);

        var tokenDescriptor = new JwtSecurityToken(
            issuer: configuration.GetValue<string>("Jwt:Issuer"),
            audience: configuration.GetValue<string>("Jwt:Audience"),
            expires: DateTime.UtcNow.AddMinutes(15),
            claims: claims,
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);
    }

    private string GenerateRefreshToken()
    {
        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    private ClaimsPrincipal? GetPrincipalFromExpiredToken(string? token)
    {
        var tokenKey = configuration.GetValue<string>("Jwt:Key");
        if (string.IsNullOrEmpty(tokenKey)) throw new InvalidOperationException("Jwt:Key is missing");

        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = false,
            ValidateIssuer = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tokenKey)),
            ValidateLifetime = false
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        try
        {
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken securityToken);
            if (securityToken is not JwtSecurityToken jwtSecurityToken || !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha512, StringComparison.InvariantCultureIgnoreCase))
            {
                return null;
            }
            return principal;
        }
        catch
        {
            return null;
        }
    }
}
