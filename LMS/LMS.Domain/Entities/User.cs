using LMS.Domain.Enums;

namespace LMS.Domain.Entities;

/// <summary>
/// Represents a system user (employee, manager, HR admin, or super admin).
/// </summary>
public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public string? AzureAdOid { get; set; }
    public UserRole Role { get; set; } = UserRole.Employee;
    public Guid? DepartmentId { get; set; }
    public bool IsActive { get; set; } = true;
    public short FailedLoginCount { get; set; } = 0;
    public DateTime? LockoutUntil { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
