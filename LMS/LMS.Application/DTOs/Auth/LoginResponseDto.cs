namespace LMS.Application.DTOs.Auth;

/// <summary>Tokens returned on successful login.</summary>
public class LoginResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; } // seconds
}
