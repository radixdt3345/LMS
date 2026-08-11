using LMS.Domain.Entities;
using LMS.Domain.Enums;
using LMS.Infrastructure.Data;
using LMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LMS.Tests.Unit.Reporting;

[Trait("Category", "Unit")]
public class ReportServiceTests
{
    private static LmsDbContext BuildDb(string name)
    {
        var opts = new DbContextOptionsBuilder<LmsDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new LmsDbContext(opts);
    }

    // ── UT-57 ─────────────────────────────────────────────────────────────────
    // GetUtilizationAsync: 3 approved requests in dept A (2d each)
    // → 1 row: DeptName="Engineering", TotalLeaveDays=6, AvgLeaveDaysPerEmployee=2.0

    [Fact]
    public async Task UT57_GetUtilizationAsync_ThreeApprovedRequestsSameDept_ReturnsCorrectAggregation()
    {
        await using var db = BuildDb($"ut57-{Guid.NewGuid()}");

        var deptId      = Guid.NewGuid();
        var leaveTypeId = Guid.NewGuid();
        var emp1        = Guid.NewGuid();
        var emp2        = Guid.NewGuid();
        var emp3        = Guid.NewGuid();

        db.Departments.Add(new Department
        {
            Id = deptId, Name = "Engineering", IsActive = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        db.LeaveTypes.Add(new LeaveType
        {
            Id = leaveTypeId, Name = "Annual", IsActive = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        db.Users.AddRange(
            new User { Id = emp1, Email = "e1@t.com", DepartmentId = deptId, IsActive = true, Role = UserRole.Employee, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new User { Id = emp2, Email = "e2@t.com", DepartmentId = deptId, IsActive = true, Role = UserRole.Employee, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new User { Id = emp3, Email = "e3@t.com", DepartmentId = deptId, IsActive = true, Role = UserRole.Employee, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        db.LeaveRequests.AddRange(
            new LeaveRequest { Id = Guid.NewGuid(), EmployeeId = emp1, LeaveTypeId = leaveTypeId, StartDate = new DateOnly(2026, 1, 10), EndDate = new DateOnly(2026, 1, 11), ComputedDays = 2m, Status = LeaveRequestStatus.Approved, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new LeaveRequest { Id = Guid.NewGuid(), EmployeeId = emp2, LeaveTypeId = leaveTypeId, StartDate = new DateOnly(2026, 2, 5),  EndDate = new DateOnly(2026, 2, 6),  ComputedDays = 2m, Status = LeaveRequestStatus.Approved, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new LeaveRequest { Id = Guid.NewGuid(), EmployeeId = emp3, LeaveTypeId = leaveTypeId, StartDate = new DateOnly(2026, 3, 1),  EndDate = new DateOnly(2026, 3, 2),  ComputedDays = 2m, Status = LeaveRequestStatus.Approved, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var svc    = new ReportService(db);
        var result = await svc.GetUtilizationAsync(2026);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.Rows);
        var row = result.Value.Rows[0];
        Assert.Equal("Engineering", row.DeptName);
        Assert.Equal(6m,   row.TotalLeaveDays);
        Assert.Equal(3,    row.TotalEmployees);
        Assert.Equal(2.0m, row.AvgLeaveDaysPerEmployee);
    }

    // ── UT-58 ─────────────────────────────────────────────────────────────────
    // GetTrendsAsync: 2 approved in month 1, 1 rejected in month 2
    // → rows[0].ApprovedCount=2, rows[1].RejectedCount=1

    [Fact]
    public async Task UT58_GetTrendsAsync_ApprovedAndRejectedAcrossMonths_ReturnsCorrectCounts()
    {
        await using var db = BuildDb($"ut58-{Guid.NewGuid()}");

        var leaveTypeId = Guid.NewGuid();
        var emp         = Guid.NewGuid();

        db.LeaveTypes.Add(new LeaveType
        {
            Id = leaveTypeId, Name = "Annual", IsActive = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        db.Users.Add(new User
        {
            Id = emp, Email = "e@t.com", IsActive = true, Role = UserRole.Employee,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        // Month 1 (July 2026): 2 approved
        db.LeaveRequests.AddRange(
            new LeaveRequest { Id = Guid.NewGuid(), EmployeeId = emp, LeaveTypeId = leaveTypeId, StartDate = new DateOnly(2026, 7, 1),  EndDate = new DateOnly(2026, 7, 2),  ComputedDays = 1m, Status = LeaveRequestStatus.Approved, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new LeaveRequest { Id = Guid.NewGuid(), EmployeeId = emp, LeaveTypeId = leaveTypeId, StartDate = new DateOnly(2026, 7, 10), EndDate = new DateOnly(2026, 7, 11), ComputedDays = 1m, Status = LeaveRequestStatus.Approved, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
        // Month 2 (August 2026): 1 rejected
            new LeaveRequest { Id = Guid.NewGuid(), EmployeeId = emp, LeaveTypeId = leaveTypeId, StartDate = new DateOnly(2026, 8, 1),  EndDate = new DateOnly(2026, 8, 1),  ComputedDays = 1m, Status = LeaveRequestStatus.Rejected, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var svc    = new ReportService(db);
        var result = await svc.GetTrendsAsync(3);

        Assert.True(result.IsSuccess);
        var rows = result.Value!.Rows;
        Assert.Equal(2, rows.Count);
        var jul = rows.First(r => r.YearMonth == "2026-07");
        var aug = rows.First(r => r.YearMonth == "2026-08");
        Assert.Equal(2, jul.ApprovedCount);
        Assert.Equal(1, aug.RejectedCount);
    }

    // ── UT-59 ─────────────────────────────────────────────────────────────────
    // GetComplianceAsync: 5 employees, 3 have submitted requests
    // → SubmissionRatePercent=60.0, TotalEmployees=5, EmployeesWithAtLeastOneRequest=3

    [Fact]
    public async Task UT59_GetComplianceAsync_FiveEmployeesThreeWithRequests_Returns60Percent()
    {
        await using var db = BuildDb($"ut59-{Guid.NewGuid()}");

        var leaveTypeId = Guid.NewGuid();
        db.LeaveTypes.Add(new LeaveType
        {
            Id = leaveTypeId, Name = "Annual", IsActive = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });

        var empIds = Enumerable.Range(1, 5).Select(_ => Guid.NewGuid()).ToList();
        db.Users.AddRange(empIds.Select(id => new User
        {
            Id = id, Email = $"{id}@t.com", IsActive = true, Role = UserRole.Employee,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        }));

        // Only 3 of the 5 employees have leave requests
        db.LeaveRequests.AddRange(
            new LeaveRequest { Id = Guid.NewGuid(), EmployeeId = empIds[0], LeaveTypeId = leaveTypeId, StartDate = new DateOnly(2026, 1, 5), EndDate = new DateOnly(2026, 1, 5), ComputedDays = 1m, Status = LeaveRequestStatus.Approved, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new LeaveRequest { Id = Guid.NewGuid(), EmployeeId = empIds[1], LeaveTypeId = leaveTypeId, StartDate = new DateOnly(2026, 2, 5), EndDate = new DateOnly(2026, 2, 5), ComputedDays = 1m, Status = LeaveRequestStatus.Pending,  CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new LeaveRequest { Id = Guid.NewGuid(), EmployeeId = empIds[2], LeaveTypeId = leaveTypeId, StartDate = new DateOnly(2026, 3, 5), EndDate = new DateOnly(2026, 3, 5), ComputedDays = 1m, Status = LeaveRequestStatus.Rejected, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var svc    = new ReportService(db);
        var result = await svc.GetComplianceAsync();

        Assert.True(result.IsSuccess);
        var dto = result.Value!;
        Assert.Equal(60.0m, dto.SubmissionRatePercent);
        Assert.Equal(5,     dto.TotalEmployees);
        Assert.Equal(3,     dto.EmployeesWithAtLeastOneRequest);
    }

    // ── UT-60 ─────────────────────────────────────────────────────────────────
    // GetEmployeeDashboardAsync: 2 leave types with balances, 2 requests (1 approved + 1 pending)
    // → Balances.Count=2, Annual.Allocated=20 / Used=5 / Available=15, PendingCount=1

    [Fact]
    public async Task UT60_GetEmployeeDashboardAsync_ReturnsBalancesAndRecentRequests()
    {
        await using var db = BuildDb($"ut60-{Guid.NewGuid()}");

        var employeeId   = Guid.NewGuid();
        var annualTypeId = Guid.NewGuid();
        var sickTypeId   = Guid.NewGuid();
        var currentYear  = (short)DateTime.UtcNow.Year;

        db.LeaveTypes.AddRange(
            new LeaveType { Id = annualTypeId, Name = "Annual", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new LeaveType { Id = sickTypeId,   Name = "Sick",   IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        db.Users.Add(new User
        {
            Id = employeeId, Email = "emp@t.com", IsActive = true, Role = UserRole.Employee,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        db.LeaveBalances.AddRange(
            new LeaveBalance { Id = Guid.NewGuid(), UserId = employeeId, LeaveTypeId = annualTypeId, Year = currentYear, AllocatedDays = 20m, UsedDays = 5m, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new LeaveBalance { Id = Guid.NewGuid(), UserId = employeeId, LeaveTypeId = sickTypeId,   Year = currentYear, AllocatedDays = 10m, UsedDays = 2m, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        db.LeaveRequests.AddRange(
            new LeaveRequest { Id = Guid.NewGuid(), EmployeeId = employeeId, LeaveTypeId = annualTypeId, StartDate = new DateOnly(2026, 3, 1), EndDate = new DateOnly(2026, 3, 2), ComputedDays = 2m, Status = LeaveRequestStatus.Approved, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new LeaveRequest { Id = Guid.NewGuid(), EmployeeId = employeeId, LeaveTypeId = sickTypeId,   StartDate = new DateOnly(2026, 4, 1), EndDate = new DateOnly(2026, 4, 1), ComputedDays = 1m, Status = LeaveRequestStatus.Pending,  CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var svc    = new ReportService(db);
        var result = await svc.GetEmployeeDashboardAsync(employeeId);

        Assert.True(result.IsSuccess);
        var dto = result.Value!;

        // Balances: both leave types present
        Assert.Equal(2, dto.Balances.Count);
        var annual = dto.Balances.First(b => b.LeaveTypeName == "Annual");
        Assert.Equal(20m, annual.Allocated);
        Assert.Equal(5m,  annual.Used);
        Assert.Equal(15m, annual.Available);
        var sick = dto.Balances.First(b => b.LeaveTypeName == "Sick");
        Assert.Equal(10m, sick.Allocated);
        Assert.Equal(2m,  sick.Used);
        Assert.Equal(8m,  sick.Available);

        // RecentRequests: both requests visible (service takes top 10)
        Assert.Equal(2, dto.RecentRequests.Count);

        // PendingCount: only the Pending request counts
        Assert.Equal(1, dto.PendingCount);
    }

    // ── UT-61 ─────────────────────────────────────────────────────────────────
    // GetManagerDashboardAsync: manager with 2 direct reports, 2 pending + 1 approved request
    // → TeamPendingRequests.Count=2 (pending only), TeamSize=2, all statuses are "Pending"

    [Fact]
    public async Task UT61_GetManagerDashboardAsync_ReturnsPendingRequestsAndTeamSize()
    {
        await using var db = BuildDb($"ut61-{Guid.NewGuid()}");

        var managerId   = Guid.NewGuid();
        var emp1Id      = Guid.NewGuid();
        var emp2Id      = Guid.NewGuid();
        var leaveTypeId = Guid.NewGuid();

        db.LeaveTypes.Add(new LeaveType
        {
            Id = leaveTypeId, Name = "Annual", IsActive = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        db.Users.AddRange(
            new User { Id = managerId, Email = "mgr@t.com",  IsActive = true, Role = UserRole.Manager,  CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new User { Id = emp1Id,    Email = "e1@t.com",   IsActive = true, Role = UserRole.Employee, ManagerId = managerId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new User { Id = emp2Id,    Email = "e2@t.com",   IsActive = true, Role = UserRole.Employee, ManagerId = managerId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        db.LeaveRequests.AddRange(
            // 2 pending requests from the 2 direct reports
            new LeaveRequest { Id = Guid.NewGuid(), EmployeeId = emp1Id, LeaveTypeId = leaveTypeId, StartDate = new DateOnly(2026, 5, 1), EndDate = new DateOnly(2026, 5, 2), ComputedDays = 2m, Status = LeaveRequestStatus.Pending,  CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new LeaveRequest { Id = Guid.NewGuid(), EmployeeId = emp2Id, LeaveTypeId = leaveTypeId, StartDate = new DateOnly(2026, 5, 5), EndDate = new DateOnly(2026, 5, 6), ComputedDays = 2m, Status = LeaveRequestStatus.Pending,  CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            // 1 approved request — must NOT appear in TeamPendingRequests
            new LeaveRequest { Id = Guid.NewGuid(), EmployeeId = emp1Id, LeaveTypeId = leaveTypeId, StartDate = new DateOnly(2026, 4, 1), EndDate = new DateOnly(2026, 4, 1), ComputedDays = 1m, Status = LeaveRequestStatus.Approved, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var svc    = new ReportService(db);
        var result = await svc.GetManagerDashboardAsync(managerId);

        Assert.True(result.IsSuccess);
        var dto = result.Value!;

        // Only pending requests surfaced
        Assert.Equal(2, dto.TeamPendingRequests.Count);
        Assert.All(dto.TeamPendingRequests, r => Assert.Equal("Pending", r.Status));

        // TeamSize reflects direct-report count (active employees with ManagerId == managerId)
        Assert.Equal(2, dto.TeamSize);
    }
}
