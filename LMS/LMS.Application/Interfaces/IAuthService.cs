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
}
