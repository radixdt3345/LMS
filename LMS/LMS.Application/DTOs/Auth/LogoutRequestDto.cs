namespace LMS.Application.DTOs.Auth;

/// <summary>Payload for POST /api/v1/auth/logout.</summary>
public class LogoutRequestDto
{
    /// <summary>The raw refresh token to revoke.</summary>
    public string RefreshToken { get; set; } = string.Empty;
}
