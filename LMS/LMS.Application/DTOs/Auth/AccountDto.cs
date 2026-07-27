namespace LMS.Application.DTOs.Auth;

/// <summary>
/// User account summary returned by GET /api/v1/auth/accounts.
/// Includes lockout status so HRAdmin can identify and unlock affected accounts.
/// </summary>
public class AccountDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    /// <summary>True when LockoutUntil is set and still in the future (UTC).</summary>
    public bool IsLocked { get; set; }
    public DateTime? LockoutUntil { get; set; }
    public short FailedLoginCount { get; set; }
}
