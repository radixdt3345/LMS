using System.Security.Cryptography;
using System.Text;
using LMS.Application.DTOs.Auth;
using LMS.Application.Interfaces;
using LMS.Application.Settings;
using LMS.Domain.Common;
using LMS.Domain.Entities;
using LMS.Domain.Enums;
using LMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using BC = BCrypt.Net.BCrypt;

namespace LMS.Infrastructure.Services;

/// <summary>
/// Handles local email/password login, Azure AD SSO callback, token refresh, and logout.
/// Returns Result&lt;T&gt; for all expected failure cases — never throws for business logic errors.
/// Kept in LMS.Infrastructure (not LMS.Application) to avoid a circular project reference.
/// </summary>
public class AuthService : IAuthService
{
    private readonly LmsDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly IMsalAuthProvider _msalAuthProvider;
    private readonly JwtSettings _jwt;
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(30);

    public AuthService(
        LmsDbContext db,
        ITokenService tokenService,
        IOptions<JwtSettings> jwt,
        IMsalAuthProvider msalAuthProvider)
    {
        _db = db;
        _tokenService = tokenService;
        _jwt = jwt.Value;
        _msalAuthProvider = msalAuthProvider;
    }

    /// <summary>
    /// Validates credentials, enforces lockout, issues JWT + refresh token on success.
    /// FR-3: local email/password login. FR-4: JWT issuance. FR-7: account lockout.
    /// </summary>
    public async Task<Result<LoginResponseDto>> LoginAsync(
        LoginRequestDto dto, CancellationToken ct = default)
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
                return Result<LoginResponseDto>.Failure(
                    "Account locked after too many failed attempts.", 423);
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

    /// <summary>
    /// Azure AD SSO callback: exchanges authorization code via MSAL, performs OID-first
    /// user lookup with email fallback for linking, auto-provisions new users as Employee.
    /// FR-5: SSO. FR-6: account linking.
    /// </summary>
    public async Task<Result<LoginResponseDto>> SsoCallbackAsync(
        string code, CancellationToken ct = default)
    {
        string oid;
        string email;
        try
        {
            (oid, email) = await _msalAuthProvider.ExchangeCodeAsync(code, ct);
        }
        catch (Exception)
        {
            return Result<LoginResponseDto>.Failure(
                "SSO authentication failed. Invalid or expired authorization code.", 400);
        }

        // OID-first lookup
        var user = await _db.Users.FirstOrDefaultAsync(u => u.AzureAdOid == oid, ct);

        if (user is null)
        {
            // Email fallback — link OID to existing local account (FR-6)
            user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
            if (user is not null)
            {
                user.AzureAdOid = oid;
                user.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                // Auto-provision new user as Employee (FR-5)
                user = new User
                {
                    Id = Guid.NewGuid(),
                    Email = email,
                    AzureAdOid = oid,
                    Role = UserRole.Employee,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                };
                _db.Users.Add(user);
            }
            await _db.SaveChangesAsync(ct);
        }

        if (!user.IsActive)
            return Result<LoginResponseDto>.Failure("Account is inactive.", 403);

        var accessToken = _tokenService.IssueAccessToken(user);
        var refreshToken = await _tokenService.IssueRefreshTokenAsync(user.Id, ct);

        return Result<LoginResponseDto>.Success(new LoginResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = _jwt.AccessTokenExpiryMinutes * 60,
        });
    }

    /// <summary>
    /// Validates the refresh token hash, rotates on use (revokes old, issues new pair).
    /// Returns 401 for expired or revoked tokens. FR-8.
    /// </summary>
    public async Task<Result<LoginResponseDto>> RefreshTokenAsync(
        string refreshToken, CancellationToken ct = default)
    {
        var hash = HashToken(refreshToken);

        var token = await _db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (token is null)
            return Result<LoginResponseDto>.Failure("Invalid refresh token.", 401);

        if (token.RevokedAt.HasValue)
            return Result<LoginResponseDto>.Failure("Refresh token has been revoked.", 401);

        if (token.ExpiresAt <= DateTime.UtcNow)
            return Result<LoginResponseDto>.Failure("Refresh token has expired.", 401);

        if (!token.User.IsActive)
            return Result<LoginResponseDto>.Failure("Account is inactive.", 403);

        // Rotate: revoke the old token, issue a new pair
        token.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var newAccessToken = _tokenService.IssueAccessToken(token.User);
        var newRefreshToken = await _tokenService.IssueRefreshTokenAsync(token.User.Id, ct);

        return Result<LoginResponseDto>.Success(new LoginResponseDto
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken,
            ExpiresIn = _jwt.AccessTokenExpiryMinutes * 60,
        });
    }

    /// <summary>
    /// Revokes the refresh token (sets revoked_at). Idempotent — already-revoked tokens
    /// are treated as success. FR-9.
    /// </summary>
    public async Task<Result<bool>> LogoutAsync(
        string refreshToken, CancellationToken ct = default)
    {
        var hash = HashToken(refreshToken);

        var token = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (token is null)
            return Result<bool>.Failure("Invalid refresh token.", 400);

        if (!token.RevokedAt.HasValue)
        {
            token.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        return Result<bool>.Success(true);
    }

    // ── Private helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// SHA-256 hash of the raw token string. Matches the hash stored by TokenService.
    /// </summary>
    private static string HashToken(string rawToken) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
