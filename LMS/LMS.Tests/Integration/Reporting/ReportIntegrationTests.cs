using LMS.Domain.Entities;
using LMS.Domain.Enums;
using LMS.Infrastructure.Data;
using LMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LMS.Tests.Integration.Reporting;

[Trait("Category", "Integration")]
public class ReportIntegrationTests
{
    private static LmsDbContext BuildDb(string name)
    {
        var opts = new DbContextOptionsBuilder<LmsDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new LmsDbContext(opts);
    }

    // ── IT-47 ────────────────────────────────────────────────────────────────
    // GetUtilizationAsync with seeded EF InMemory data — dept grouping correct

    [Fact]
    public async Task IT47_GetUtilizationAsync_MultiDeptSeededData_GroupsCorrectly()
    {
        await using var db = BuildDb($"it47-{Guid.NewGuid()}");

        var deptA       = Guid.NewGuid();
        var deptB       = Guid.NewGuid();
        var leaveTypeId = Guid.NewGuid();
        var empA1       = Guid.NewGuid();
        var empA2       = Guid.NewGuid();
        var empB1       = Guid.NewGuid();

        db.Departments.AddRange(
            new Department { Id = deptA, Name = "Engineering", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Department { Id = deptB, Name = "Marketing",   IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        db.LeaveTypes.Add(new LeaveType
        {
            Id = leaveTypeId, Name = "Annual", IsActive = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        db.Users.AddRange(
            new User { Id = empA1, Email = "a1@t.com", DepartmentId = deptA, IsActive = true, Role = UserRole.Employee, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new User { Id = empA2, Email = "a2@t.com", DepartmentId = deptA, IsActive = true, Role = UserRole.Employee, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new User { Id = empB1, Email = "b1@t.com", DepartmentId = deptB, IsActive = true, Role = UserRole.Employee, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        db.LeaveRequests.AddRange(
            new LeaveRequest { Id = Guid.NewGuid(), EmployeeId = empA1, LeaveTypeId = leaveTypeId, StartDate = new DateOnly(2026, 4, 1), EndDate = new DateOnly(2026, 4, 2), ComputedDays = 3m, Status = LeaveRequestStatus.Approved, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new LeaveRequest { Id = Guid.NewGuid(), EmployeeId = empA2, LeaveTypeId = leaveTypeId, StartDate = new DateOnly(2026, 4, 5), EndDate = new DateOnly(2026, 4, 6), ComputedDays = 2m, Status = LeaveRequestStatus.Approved, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new LeaveRequest { Id = Guid.NewGuid(), EmployeeId = empB1, LeaveTypeId = leaveTypeId, StartDate = new DateOnly(2026, 5, 1), EndDate = new DateOnly(2026, 5, 1), ComputedDays = 1m, Status = LeaveRequestStatus.Approved, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var svc    = new ReportService(db);
        var result = await svc.GetUtilizationAsync(2026);

        Assert.True(result.IsSuccess);
        var rows = result.Value!.Rows;
        Assert.Equal(2, rows.Count);

        var eng = rows.Single(r => r.DeptName == "Engineering");
        Assert.Equal(5m, eng.TotalLeaveDays);
        Assert.Equal(2,  eng.TotalEmployees);
        Assert.Equal(2.5m, eng.AvgLeaveDaysPerEmployee);

        var mkt = rows.Single(r => r.DeptName == "Marketing");
        Assert.Equal(1m, mkt.TotalLeaveDays);
        Assert.Equal(1,  mkt.TotalEmployees);
    }

    // ── IT-48 ────────────────────────────────────────────────────────────────
    // ExportCsvAsync — IAsyncEnumerable<string> non-empty, first item is header containing "Department"

    [Fact]
    public async Task IT48_ExportCsvAsync_UtilizationType_FirstLineIsHeaderWithDepartment()
    {
        await using var db = BuildDb($"it48-{Guid.NewGuid()}");

        var deptId      = Guid.NewGuid();
        var leaveTypeId = Guid.NewGuid();
        var empId       = Guid.NewGuid();

        db.Departments.Add(new Department
        {
            Id = deptId, Name = "IT", IsActive = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        db.LeaveTypes.Add(new LeaveType
        {
            Id = leaveTypeId, Name = "Casual", IsActive = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        db.Users.Add(new User
        {
            Id = empId, Email = "emp@t.com", DepartmentId = deptId,
            IsActive = true, Role = UserRole.Employee,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        db.LeaveRequests.Add(new LeaveRequest
        {
            Id = Guid.NewGuid(), EmployeeId = empId, LeaveTypeId = leaveTypeId,
            StartDate = new DateOnly(DateTime.UtcNow.Year, 1, 10),
            EndDate   = new DateOnly(DateTime.UtcNow.Year, 1, 10),
            ComputedDays = 1m, Status = LeaveRequestStatus.Approved,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var svc   = new ReportService(db);
        var lines = new List<string>();

        await foreach (var line in svc.ExportCsvAsync("utilization"))
            lines.Add(line);

        Assert.NotEmpty(lines);
        Assert.Contains("Department", lines[0], StringComparison.OrdinalIgnoreCase);
        Assert.True(lines.Count >= 2, "Expected header + at least one data row");
    }
}
