namespace LMS.Application.DTOs.Auth;

/// <summary>Request body for local email/password login.</summary>
public class LoginRequestDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
