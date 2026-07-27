using LMS.Application.DTOs.Auth;
using LMS.Domain.Common;

namespace LMS.Application.Interfaces;

/// <summary>Handles authentication flows (local login, SSO, refresh, logout).</summary>
public interface IAuthService
{
    Task<Result<LoginResponseDto>> LoginAsync(LoginRequestDto dto, CancellationToken ct = default);
}
