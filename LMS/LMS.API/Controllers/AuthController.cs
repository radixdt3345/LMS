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

    /// <summary>
    /// GET /api/v1/auth/sso/callback?code=&amp;state= — Azure AD SSO callback.
    /// Exchanges authorization code for JWT + refresh token.
    /// New users are auto-provisioned as Employee. Existing users found by OID,
    /// or linked by email when OID not yet set.
    /// FR-5, FR-6.
    /// </summary>
    [HttpGet("sso/callback")]
    public async Task<IActionResult> SsoCallback([FromQuery] string code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest(new
            {
                success = false,
                error = new { message = "Authorization code is required." }
            });

        var result = await _authService.SsoCallbackAsync(code, ct);

        if (!result.IsSuccess)
            return StatusCode(result.StatusCode, new
            {
                success = false,
                error = new { message = result.Error }
            });

        return Ok(new { success = true, data = result.Value });
    }
}
