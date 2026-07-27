using LMS.Application.DTOs.Auth;
using LMS.Domain.Common;

namespace LMS.Application.Interfaces;

/// <summary>Handles authentication flows (local login, Azure AD SSO, refresh, logout).</summary>
public interface IAuthService
{
    Task<Result<LoginResponseDto>> LoginAsync(LoginRequestDto dto, CancellationToken ct = default);

    /// <summary>
    /// Azure AD SSO callback. Exchanges code via MSAL, performs OID-first user lookup
    /// with email fallback for linking, auto-provisions new users as Employee.
    /// FR-5, FR-6.
    /// </summary>
    Task<Result<LoginResponseDto>> SsoCallbackAsync(string code, CancellationToken ct = default);

    /// <summary>
    /// Validates the refresh token hash, rotates it (revokes old, issues new JWT + refresh token).
    /// Returns 401 for expired or revoked tokens. FR-8.
    /// </summary>
    Task<Result<LoginResponseDto>> RefreshTokenAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>
    /// Revokes the refresh token from the database (sets revoked_at). FR-9.
    /// </summary>
    Task<Result<bool>> LogoutAsync(string refreshToken, CancellationToken ct = default);
}
