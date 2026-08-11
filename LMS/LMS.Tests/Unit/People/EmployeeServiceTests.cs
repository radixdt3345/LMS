using LMS.Application.DTOs.People;
using LMS.Application.Interfaces;
using LMS.Domain.Entities;
using LMS.Domain.Enums;
using LMS.Infrastructure.Data;
using LMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace LMS.Tests.Unit.People;

[Trait("Category", "Unit")]
public class EmployeeServiceTests
{
    private static LmsDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<LmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new LmsDbContext(options);
    }

    private static EmployeeService BuildService(LmsDbContext db, IAuditService? audit = null)
    {
        audit ??= new Mock<IAuditService>().Object;
        return new EmployeeService(db, audit);
    }

    private static User MakeUser(string email, UserRole role = UserRole.Employee, Guid? managerId = null, bool isActive = true) => new()
    {
        Id        = Guid.NewGuid(),
        Email     = email,
        Role      = role,
        IsActive  = isActive,
        ManagerId = managerId,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    [Fact]
    public async Task CreateEmployeeAsync_ValidDto_CreatesRowWithCorrectFields()
    {
        await using var db = CreateInMemoryDb();
        var svc = BuildService(db);

        var dto = new CreateEmployeeDto(
            Email:        "alice@example.com",
            Password:     "Pass123!",
            AzureAdOid:   null,
            FirstName:    "Alice",
            LastName:     "Smith",
            Phone:        "+91-9876543210",
            EmployeeCode: "EMP-001",
            DepartmentId: null,
            ManagerId:    null,
            Role:         "Employee");

        var result = await svc.CreateEmployeeAsync(dto);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("alice@example.com", result.Value!.Email);
        Assert.Equal("Alice",   result.Value.FirstName);
        Assert.Equal("Smith",   result.Value.LastName);
        Assert.Equal("EMP-001", result.Value.EmployeeCode);
        Assert.Equal("Employee", result.Value.Role);
        Assert.True(result.Value.IsActive);

        var persisted = await db.Users.SingleAsync(u => u.Email == "alice@example.com");
        Assert.NotNull(persisted.PasswordHash);
        Assert.NotEqual("Pass123!", persisted.PasswordHash);
        Assert.Equal("Alice", persisted.FirstName);
        Assert.Equal("EMP-001", persisted.EmployeeCode);
    }

    [Fact]
    public async Task CreateEmployeeAsync_WithManagerId_DeriveRolePromotesManagerToManager()
    {
        await using var db = CreateInMemoryDb();
        var manager = MakeUser("manager@example.com", UserRole.Employee);
        db.Users.Add(manager);
        await db.SaveChangesAsync();

        var svc = BuildService(db);
        var dto = new CreateEmployeeDto(
            Email: "report@example.com", Password: "Pass!", AzureAdOid: null,
            FirstName: null, LastName: null, Phone: null, EmployeeCode: null,
            DepartmentId: null, ManagerId: manager.Id, Role: null);

        var result = await svc.CreateEmployeeAsync(dto);
        Assert.True(result.IsSuccess);

        var updatedManager = await db.Users.FindAsync(manager.Id);
        Assert.Equal(UserRole.Manager, updatedManager!.Role);
    }

    [Fact]
    public async Task CreateEmployeeAsync_ManagerAlreadyManager_DeriveRoleIsIdempotent()
    {
        await using var db = CreateInMemoryDb();
        var manager = MakeUser("senior@example.com", UserRole.Manager);
        db.Users.Add(manager);
        await db.SaveChangesAsync();

        var svc = BuildService(db);
        var dto = new CreateEmployeeDto(
            Email: "report2@example.com", Password: "Pass!", AzureAdOid: null,
            FirstName: null, LastName: null, Phone: null, EmployeeCode: null,
            DepartmentId: null, ManagerId: manager.Id, Role: null);

        await svc.CreateEmployeeAsync(dto);

        var updatedManager = await db.Users.FindAsync(manager.Id);
        Assert.Equal(UserRole.Manager, updatedManager!.Role);
    }

    [Fact]
    public async Task DeactivateEmployeeAsync_ActiveEmployee_SetsIsActiveFalse()
    {
        await using var db = CreateInMemoryDb();
        var user = MakeUser("active@example.com");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var svc = BuildService(db);
        var result = await svc.DeactivateEmployeeAsync(user.Id);

        Assert.True(result.IsSuccess);
        var updated = await db.Users.FindAsync(user.Id);
        Assert.False(updated!.IsActive);
    }

    [Fact]
    public async Task GetTeamAsync_Manager_ReturnsOnlyActiveDirectReports()
    {
        await using var db = CreateInMemoryDb();
        var manager     = MakeUser("mgr@example.com", UserRole.Manager);
        var reportA     = MakeUser("reportA@example.com", managerId: manager.Id);
        var reportB     = MakeUser("reportB@example.com", managerId: manager.Id);
        var inactiveRpt = MakeUser("inactive@example.com", managerId: manager.Id, isActive: false);
        var otherMgr    = MakeUser("othermgr@example.com", UserRole.Manager);
        var unrelated   = MakeUser("unrelated@example.com", managerId: otherMgr.Id);

        db.Users.AddRange(manager, reportA, reportB, inactiveRpt, otherMgr, unrelated);
        await db.SaveChangesAsync();

        var svc    = BuildService(db);
        var result = await svc.GetTeamAsync(manager.Id, manager.Id, callerIsHrAdmin: false);

        Assert.True(result.IsSuccess);
        var team = result.Value!.ToList();
        Assert.Equal(2, team.Count);
        Assert.Contains(team, e => e.Email == "reportA@example.com");
        Assert.Contains(team, e => e.Email == "reportB@example.com");
        Assert.DoesNotContain(team, e => e.Email == "inactive@example.com");
        Assert.DoesNotContain(team, e => e.Email == "unrelated@example.com");
    }
}
