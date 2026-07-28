using LMS.Domain.Enums;

namespace LMS.Domain.Entities;

/// <summary>
/// Represents a system user (employee, manager, HR admin, or super admin).
/// Profile columns (first_name, last_name, phone, join_date, manager_id, employee_code)
/// were added by migration AddUserProfileColumns (PEOPLE-DB-002, FR-12 to FR-20).
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

    // Profile columns added by PEOPLE-DB-002 (FR-12 to FR-20)
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Phone { get; set; }
    public DateOnly? JoinDate { get; set; }
    public Guid? ManagerId { get; set; }
    public string? EmployeeCode { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public User? Manager { get; set; }
    public ICollection<User> DirectReports { get; set; } = new List<User>();
}
