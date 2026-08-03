using LMS.Application.DTOs.CompOff;
using LMS.Application.DTOs.LeaveRequest;
using LMS.Domain.Entities;
using LMS.Domain.Enums;
using LMS.Infrastructure.Data;
using LMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LMS.Tests.Integration.CompOff;

/// <summary>
/// Integration tests for the compensatory-off request lifecycle.
/// Uses EF Core InMemory — no PostgreSQL required.
///
/// IT-33: Submit comp-off (8h) + approve → CompOffCredit.CreditDays=1.0, balance +1.0.
/// IT-34: Submit comp-off (4h) + approve → CompOffCredit.CreditDays=0.5, balance +0.5.
/// IT-35: Seed comp-off balance 1.0d; submit leave using comp-off type → balance decreases.
/// IT-36: Seed CompOffCredit with ExpiresAt=yesterday; ExpireCompOffCredits marks fully used.
///
/// Run: dotnet test --filter Category=Integration
/// </summary>
[Trait("Category", "Integration")]
public class CompOffIntegrationTests
{
    // ── DB factory ────────────────────────────────────────────────────────────

    private static LmsDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<LmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new LmsDbContext(opts);
    }

    // ── Service factories ─────────────────────────────────────────────────────

    private static CompOffRequestService BuildCompOffSvc(LmsDbContext db)
    {
        var audit      = new AuditService(db, NullLogger<AuditService>.Instance);
        var holidaySvc = new HolidayService(db);
        var creditSvc  = new CompOffCreditService(db);
        return new CompOffRequestService(db, holidaySvc, audit, creditSvc);
    }

    private static LeaveRequestService BuildLeaveSvc(LmsDbContext db)
    {
        var audit    = new AuditService(db, NullLogger<AuditService>.Instance);
        var balance  = new LeaveBalanceService(db);
        var approval = new ApprovalService(db);
        return new LeaveRequestService(db, audit, balance, approval);
    }

    // ── Seed helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Seeds one HR Admin and one Employee.
    /// Returns both so callers can use the HR Admin as approver.
    /// </summary>
    private static (User employee, User hrAdmin) SeedUsers(LmsDbContext db)
    {
        var hrAdmin = new User
        {
            Id        = Guid.NewGuid(),
            Email     = $"hr-{Guid.NewGuid()}@test.com",
            Role      = UserRole.HRAdmin,
            IsActive  = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        var employee = new User
        {
            Id        = Guid.NewGuid(),
            Email     = $"emp-{Guid.NewGuid()}@test.com",
            Role      = UserRole.Employee,
            IsActive  = true,
            ManagerId = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Users.AddRange(hrAdmin, employee);
        db.SaveChanges();
        return (employee, hrAdmin);
    }

    /// <summary>
    /// Seeds the "Comp Off" leave type (AccrualType=OneTime, Name contains "Comp").
    /// CompOffCreditService discovers it by AccrualType + Name substring.
    /// </summary>
    private static LeaveType SeedCompOffType(LmsDbContext db)
    {
        var lt = new LeaveType
        {
            Id               = Guid.NewGuid(),
            Name             = "Comp Off",
            MaxDaysPerYear   = null,
            AccrualType      = AccrualType.OneTime,
            RequiresDocument = false,
            IsActive         = true,
            CreatedAt        = DateTime.UtcNow,
            UpdatedAt        = DateTime.UtcNow,
        };
        db.LeaveTypes.Add(lt);
        db.SaveChanges();
        return lt;
    }

    // ── IT-33 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// IT-33: Submit comp-off request (8 worked hours) and approve.
    /// Assert CompOffCredit row with CreditDays = 1.0 and the employee's
    /// comp-off leave balance is increased by 1.0.
    /// </summary>
    [Fact]
    public async Task IT33_ApproveCompOff8Hours_CreditDays1_BalanceIncreases1()
    {
        await using var db = CreateDb();
        var compOffSvc = BuildCompOffSvc(db);
        var (employee, hrAdmin) = SeedUsers(db);
        var compOffType = SeedCompOffType(db);

        // Sunday 26 Jul 2026 — non-working day (HolidayService returns false for weekends)
        var workedDate = new DateOnly(2026, 7, 26);

        var submit = await compOffSvc.SubmitAsync(employee.Id, new CreateCompOffRequestDto
        {
            WorkedDate  = workedDate,
            WorkedHours = 8m,
        });
        Assert.True(submit.IsSuccess, $"Submit failed: {submit.Error}");

        var approve = await compOffSvc.ApproveAsync(submit.Value!.Id, hrAdmin.Id);
        Assert.True(approve.IsSuccess, $"Approve failed: {approve.Error}");

        db.ChangeTracker.Clear();

        // Assert CompOffCredit row with CreditDays = 1.0 and correct expiry
        var credit = await db.CompOffCredits
            .FirstAsync(c => c.EmployeeId == employee.Id);
        Assert.Equal(1.0m, credit.CreditDays);
        Assert.Equal(workedDate.AddDays(180), credit.ExpiresAt);
        Assert.Equal(0m, credit.UsedDays);

        // Assert comp-off leave balance increased by 1.0
        var balance = await db.LeaveBalances
            .FirstAsync(b => b.UserId == employee.Id && b.LeaveTypeId == compOffType.Id);
        Assert.Equal(1.0m, balance.AllocatedDays);
        Assert.Equal(0m, balance.UsedDays);
    }

    // ── IT-34 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// IT-34: Submit comp-off request (4 worked hours) and approve.
    /// Assert CompOffCredit.CreditDays = 0.5 and comp-off leave balance +0.5.
    /// </summary>
    [Fact]
    public async Task IT34_ApproveCompOff4Hours_CreditDays0_5_BalanceIncreases0_5()
    {
        await using var db = CreateDb();
        var compOffSvc = BuildCompOffSvc(db);
        var (employee, hrAdmin) = SeedUsers(db);
        var compOffType = SeedCompOffType(db);

        // Saturday 1 Aug 2026 — non-working day
        var workedDate = new DateOnly(2026, 8, 1);

        var submit = await compOffSvc.SubmitAsync(employee.Id, new CreateCompOffRequestDto
        {
            WorkedDate  = workedDate,
            WorkedHours = 4m,
        });
        Assert.True(submit.IsSuccess, $"Submit failed: {submit.Error}");

        var approve = await compOffSvc.ApproveAsync(submit.Value!.Id, hrAdmin.Id);
        Assert.True(approve.IsSuccess, $"Approve failed: {approve.Error}");

        db.ChangeTracker.Clear();

        var credit = await db.CompOffCredits
            .FirstAsync(c => c.EmployeeId == employee.Id);
        Assert.Equal(0.5m, credit.CreditDays);

        var balance = await db.LeaveBalances
            .FirstAsync(b => b.UserId == employee.Id && b.LeaveTypeId == compOffType.Id);
        Assert.Equal(0.5m, balance.AllocatedDays);
    }

    // ── IT-35 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// IT-35: Seed comp-off leave balance (1.0d). Submit a 1-day leave request
    /// using the comp-off leave type. Assert balance.UsedDays increased by 1.0.
    /// </summary>
    [Fact]
    public async Task IT35_UseCompOffBalance_Submit1DayLeave_BalanceUsedDaysIncreases()
    {
        await using var db = CreateDb();
        var leaveSvc = BuildLeaveSvc(db);
        var (employee, _) = SeedUsers(db);
        var compOffType = SeedCompOffType(db);

        // Seed 1.0d comp-off balance so the deduction can succeed
        db.LeaveBalances.Add(new LeaveBalance
        {
            Id            = Guid.NewGuid(),
            UserId        = employee.Id,
            LeaveTypeId   = compOffType.Id,
            Year          = (short)DateTime.UtcNow.Year,
            AllocatedDays = 1.0m,
            UsedDays      = 0m,
            CreatedAt     = DateTime.UtcNow,
            UpdatedAt     = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        // Submit 1-day leave request on Monday 26 Oct 2026 using comp-off type
        var r = await leaveSvc.CreateRequestAsync(employee.Id, new CreateLeaveRequestDto
        {
            LeaveTypeId = compOffType.Id,
            StartDate   = new DateOnly(2026, 10, 26), // Monday
            EndDate     = new DateOnly(2026, 10, 26),
            Reason      = "IT-35 use comp-off leave",
        });
        Assert.True(r.IsSuccess);

        var submit = await leaveSvc.SubmitRequestAsync(r.Value!.Id, employee.Id);
        Assert.True(submit.IsSuccess, $"Submit failed: {submit.Error}");
        Assert.Equal(1m, submit.Value!.ComputedDays);

        db.ChangeTracker.Clear();
        var bal = await db.LeaveBalances
            .FirstAsync(b => b.UserId == employee.Id && b.LeaveTypeId == compOffType.Id);
        Assert.Equal(1.0m, bal.UsedDays);
    }

    // ── IT-36 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// IT-36: Seed a CompOffCredit with ExpiresAt = yesterday and UsedDays = 0.
    /// Run ExpireCompOffCredits(). Assert the credit's UsedDays equals CreditDays
    /// (i.e. the credit has been marked as fully consumed and is no longer usable).
    /// </summary>
    [Fact]
    public async Task IT36_ExpiredCredit_ExpireJobMarksFullyConsumed()
    {
        await using var db = CreateDb();
        var balanceSvc = new LeaveBalanceService(db);

        var employee = new User
        {
            Id        = Guid.NewGuid(),
            Email     = $"emp-{Guid.NewGuid()}@test.com",
            Role      = UserRole.Employee,
            IsActive  = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        // A CompOffRequest is needed as a FK parent for the credit row.
        var compOffRequest = new CompOffRequest
        {
            Id          = Guid.NewGuid(),
            EmployeeId  = employee.Id,
            WorkedDate  = new DateOnly(2026, 1, 4), // Sunday
            WorkedHours = 8m,
            Status      = CompOffStatus.Approved,
            CreatedAt   = DateTime.UtcNow,
            UpdatedAt   = DateTime.UtcNow,
        };

        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        var expiredCredit = new CompOffCredit
        {
            Id               = Guid.NewGuid(),
            EmployeeId       = employee.Id,
            CompOffRequestId = compOffRequest.Id,
            CreditDays       = 1.0m,
            ExpiresAt        = yesterday, // already expired
            UsedDays         = 0m,        // not yet marked consumed
            CreatedAt        = DateTime.UtcNow,
        };

        db.Users.Add(employee);
        db.CompOffRequests.Add(compOffRequest);
        db.CompOffCredits.Add(expiredCredit);
        await db.SaveChangesAsync();

        // Run the expiry job
        await balanceSvc.ExpireCompOffCredits();

        db.ChangeTracker.Clear();
        var credit = await db.CompOffCredits.FindAsync(expiredCredit.Id);
        Assert.NotNull(credit);
        // UsedDays must equal CreditDays — credit is fully consumed and no longer usable
        Assert.Equal(credit!.CreditDays, credit.UsedDays);
    }
}
