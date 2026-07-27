using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using LMS.Application.Interfaces;
using LMS.Application.Settings;
using LMS.Domain.Entities;
using LMS.Infrastructure.Data;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LMS.Infrastructure.Services;

/// <summary>
/// Issues JWT access tokens and manages refresh tokens.
/// Access tokens are returned in-memory only — never written to any persistent client store.
/// Refresh tokens are stored as SHA-256 hashes in the database.
/// </summary>
public class TokenService : ITokenService
{
    private readonly JwtSettings _jwt;
    private readonly LmsDbContext _db;

    public TokenService(IOptions<JwtSettings> jwt, LmsDbContext db)
    {
        _jwt = jwt.Value;
        _db = db;
    }

    /// <summary>
    /// Issues a signed JWT access token. Token is NOT written to any persistent store — memory only.
    /// </summary>
    public string IssueAccessToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("role", user.Role.ToString()),
            new Claim("department_id", user.DepartmentId?.ToString() ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwt.AccessTokenExpiryMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Generates a cryptographically random refresh token, stores its SHA-256 hash in the DB.
    /// Returns the raw token (sent to client once — the raw value is never stored).
    /// </summary>
    public async Task<string> IssueRefreshTokenAsync(Guid userId, CancellationToken ct = default)
    {
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = hash,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwt.RefreshTokenExpiryDays),
            CreatedAt = DateTime.UtcNow,
        };

        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync(ct);

        return rawToken;
    }
}
