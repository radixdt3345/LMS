namespace LMS.Application.DTOs.Auth;

/// <summary>Payload for POST /api/v1/auth/refresh.</summary>
public class RefreshTokenRequestDto
{
    /// <summary>The raw refresh token previously issued by the server.</summary>
    public string RefreshToken { get; set; } = string.Empty;
}
