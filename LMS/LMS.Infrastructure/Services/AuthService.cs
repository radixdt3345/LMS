using LMS.Application.DTOs.Auth;
using LMS.Application.Interfaces;
using LMS.Application.Settings;
using LMS.Domain.Common;
using LMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using BC = BCrypt.Net.BCrypt;

namespace LMS.Infrastructure.Services;

/// <summary>
/// Handles local email/password authentication with lockout enforcement.
/// Returns Result&lt;T&gt; for all expected failure cases — never throws.
/// </summary>
public class AuthService : IAuthService
{
    private readonly LmsDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly JwtSettings _jwt;
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(30);

    public AuthService(LmsDbContext db, ITokenService tokenService, IOptions<JwtSettings> jwt)
    {
        _db = db;
        _tokenService = tokenService;
        _jwt = jwt.Value;
    }

    /// <summary>
    /// Validates credentials, enforces lockout, issues JWT + refresh token on success.
    /// FR-3: local email/password login. FR-4: JWT + refresh token issuance. FR-7: account lockout.
    /// </summary>
    public async Task<Result<LoginResponseDto>> LoginAsync(LoginRequestDto dto, CancellationToken ct = default)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == dto.Email, ct);

        if (user is null || !user.IsActive)
            return Result<LoginResponseDto>.Failure("Invalid credentials.", 401);

        // Check lockout before verifying password
        if (user.LockoutUntil.HasValue && user.LockoutUntil > DateTime.UtcNow)
            return Result<LoginResponseDto>.Failure("Account is locked. Try again later.", 423);

        // Verify password hash — never log or store the raw password
        if (string.IsNullOrEmpty(user.PasswordHash) || !BC.Verify(dto.Password, user.PasswordHash))
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= MaxFailedAttempts)
            {
                user.LockoutUntil = DateTime.UtcNow.Add(LockoutDuration);
                await _db.SaveChangesAsync(ct);
                return Result<LoginResponseDto>.Failure("Account locked after too many failed attempts.", 423);
            }
            await _db.SaveChangesAsync(ct);
            return Result<LoginResponseDto>.Failure("Invalid credentials.", 401);
        }

        // Success path — reset failure counter, then issue tokens
        user.FailedLoginCount = 0;
        user.LockoutUntil = null;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var accessToken = _tokenService.IssueAccessToken(user);
        var refreshToken = await _tokenService.IssueRefreshTokenAsync(user.Id, ct);

        return Result<LoginResponseDto>.Success(new LoginResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = _jwt.AccessTokenExpiryMinutes * 60,
        });
    }
}
