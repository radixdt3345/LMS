using LMS.Domain.Entities;
using LMS.Domain.Enums;
using Xunit;

namespace LMS.Tests.Unit.People;

/// <summary>
/// Unit tests for User entity profile columns added by PEOPLE-DB-002.
/// Verifies new properties are nullable and can be set correctly.
/// IT-11 (integration test against real DB) is handled in the TEST phase.
/// </summary>
[Trait("Category", "Unit")]
public class UserEntityTests
{
    [Fact]
    public void User_ProfileColumns_DefaultToNull()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        Assert.Null(user.FirstName);
        Assert.Null(user.LastName);
        Assert.Null(user.Phone);
        Assert.Null(user.JoinDate);
        Assert.Null(user.ManagerId);
        Assert.Null(user.EmployeeCode);
    }

    [Fact]
    public void User_CanSetAllProfileColumns()
    {
        var managerId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "emp@example.com",
            Role = UserRole.Employee,
            FirstName = "Alice",
            LastName = "Smith",
            Phone = "+91-9876543210",
            JoinDate = new DateOnly(2024, 1, 15),
            ManagerId = managerId,
            EmployeeCode = "EMP001",
            DepartmentId = Guid.NewGuid(),
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };

        Assert.Equal("Alice", user.FirstName);
        Assert.Equal("Smith", user.LastName);
        Assert.Equal("+91-9876543210", user.Phone);
        Assert.Equal(new DateOnly(2024, 1, 15), user.JoinDate);
        Assert.Equal(managerId, user.ManagerId);
        Assert.Equal("EMP001", user.EmployeeCode);
    }

    [Fact]
    public void User_ManagerIdNull_RepresentsNoManager()
    {
        // AC: manager_id allows NULL (no-manager employees)
        // No-manager rule: employee.manager_id IS NULL means HR Admin is L1+L2 (UT-53)
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "noManager@example.com",
            ManagerId = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        Assert.Null(user.ManagerId);
        Assert.Null(user.Manager); // navigation also null
    }

    [Fact]
    public void User_DirectReports_DefaultsToEmptyCollection()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "mgr@example.com" };
        Assert.NotNull(user.DirectReports);
        Assert.Empty(user.DirectReports);
    }
}
