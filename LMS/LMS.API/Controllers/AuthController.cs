using LMS.Application.DTOs.Auth;
using LMS.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace LMS.API.Controllers;

/// <summary>
/// Authentication endpoints — local login, Azure AD SSO, token refresh, logout.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// POST /api/v1/auth/login — local email/password login.
    /// Returns JWT access token (body only — never written to localStorage) and refresh token.
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto dto, CancellationToken ct)
    {
        var result = await _authService.LoginAsync(dto, ct);

        if (!result.IsSuccess)
            return StatusCode(result.StatusCode, new
            {
                success = false,
                error = new { message = result.Error }
            });

        return Ok(new { success = true, data = result.Value });
    }
}
