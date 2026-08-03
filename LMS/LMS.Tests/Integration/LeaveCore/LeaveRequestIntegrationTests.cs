using LMS.Application.DTOs.LeaveRequest;
using LMS.Domain.Entities;
using LMS.Domain.Enums;
using LMS.Infrastructure.Data;
using LMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LMS.Tests.Integration.LeaveCore;

/// <summary>
/// Integration tests for the leave request lifecycle via LeaveRequestService.
/// Uses EF Core InMemory — no PostgreSQL required.
///
/// IT-21: Balance deducted correctly after 3-working-day submit.
/// IT-22: Fri–Mon submit: sandwiched Sat+Sun yield 4 computed days.
/// IT-23: Overlapping second submit returns failure.
/// IT-24: Cancel after submit restores balance to 0.
/// IT-25: Unlimited leave type bypasses balance check — succeeds at 0 balance.
/// IT-26: Past start_date sets IsRetroactive = true.
/// IT-27: RequiresDocument=true with no document URL rejects submit.
/// IT-28: Approved request revoked by HR Admin — balance restored.
/// IT-29: Single working day submit yields computed_days = 1.
/// IT-30: Single holiday day submit yields computed_days = 0 (not sandwiched).
/// IT-31: Mon–Fri with mid-week holiday sandwiched = 5 computed days.
/// IT-32: Full state machine Draft → Pending → Approved → Revoked.
///
/// Run: dotnet test --filter Category=Integration
/// </summary>
[Trait("Category", "Integration")]
public class LeaveRequestIntegrationTests
{
    // ── DB factory ────────────────────────────────────────────────────────────

    private static LmsDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<LmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new LmsDbContext(opts);
    }

    // ── Service factory ───────────────────────────────────────────────────────

    private static LeaveRequestService BuildSvc(LmsDbContext db)
    {
        var audit    = new AuditService(db, NullLogger<AuditService>.Instance);
        var balance  = new LeaveBalanceService(db);
        var approval = new ApprovalService(db);
        return new LeaveRequestService(db, audit, balance, approval);
    }

    // ── Seed helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Seeds one HR Admin (acts as L1 approver when employee has no manager),
    /// one Employee with no manager, one Annual Leave type, and a balance row.
    /// </summary>
    private static (User employee, User hrAdmin, LeaveType leaveType)
        SeedAnnualLeave(LmsDbContext db, decimal allocated = 10m)
    {
        var hrAdmin = new User
        {
            Id        = Guid.NewGuid(),
            Email     = $"hradmin-{Guid.NewGuid()}@test.com",
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
            ManagerId = null, // no manager → HR Admin is the sole L1 approver
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        var leaveType = new LeaveType
        {
            Id               = Guid.NewGuid(),
            Name             = "Annual Leave",
            MaxDaysPerYear   = 21,
            AccrualType      = AccrualType.Annual,
            RequiresDocument = false,
            IsActive         = true,
            CreatedAt        = DateTime.UtcNow,
            UpdatedAt        = DateTime.UtcNow,
        };
        db.Users.AddRange(hrAdmin, employee);
        db.LeaveTypes.Add(leaveType);
        db.LeaveBalances.Add(new LeaveBalance
        {
            Id            = Guid.NewGuid(),
            UserId        = employee.Id,
            LeaveTypeId   = leaveType.Id,
            Year          = (short)DateTime.UtcNow.Year,
            AllocatedDays = allocated,
            UsedDays      = 0m,
            CreatedAt     = DateTime.UtcNow,
            UpdatedAt     = DateTime.UtcNow,
        });
        db.SaveChanges();
        return (employee, hrAdmin, leaveType);
    }

    // ── IT-21 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// IT-21: Seed employee + AnnualLeave balance (10d). Submit leave Mon–Wed
    /// (3 working days). Assert balance.UsedDays increased by 3.
    /// </summary>
    [Fact]
    public async Task IT21_Submit3WorkingDays_BalanceUsedDaysIncreasedBy3()
    {
        await using var db = CreateDb();
        var svc = BuildSvc(db);
        var (employee, _, leaveType) = SeedAnnualLeave(db, allocated: 10m);

        // Mon 10 Aug – Wed 12 Aug 2026 (3 working days, no holidays)
        var create = await svc.CreateRequestAsync(employee.Id, new CreateLeaveRequestDto
        {
            LeaveTypeId = leaveType.Id,
            StartDate   = new DateOnly(2026, 8, 10),
            EndDate     = new DateOnly(2026, 8, 12),
            Reason      = "IT-21",
        });
        Assert.True(create.IsSuccess, $"Create failed: {create.Error}");

        var submit = await svc.SubmitRequestAsync(create.Value!.Id, employee.Id);
        Assert.True(submit.IsSuccess, $"Submit failed: {submit.Error}");
        Assert.Equal(3m, submit.Value!.ComputedDays);

        db.ChangeTracker.Clear();
        var bal = await db.LeaveBalances
            .FirstAsync(b => b.UserId == employee.Id && b.LeaveTypeId == leaveType.Id);
        Assert.Equal(3m, bal.UsedDays);
    }

    // ── IT-22 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// IT-22: Submit leave Fri 31 Jul – Mon 3 Aug 2026. Sat+Sun are sandwiched
    /// between two working days within the same request range.
    /// Assert computed_days = 4 (Fri + Sat + Sun + Mon).
    /// </summary>
    [Fact]
    public async Task IT22_FridayToMonday_SandwichedWeekend_ComputedDays4()
    {
        await using var db = CreateDb();
        var svc = BuildSvc(db);
        var (employee, _, leaveType) = SeedAnnualLeave(db, allocated: 10m);

        // Fri 31 Jul 2026 → Mon 3 Aug 2026
        // Working days: Fri(1), Mon(1)   Sandwiched non-working: Sat(1), Sun(1)   Total = 4
        var create = await svc.CreateRequestAsync(employee.Id, new CreateLeaveRequestDto
        {
            LeaveTypeId = leaveType.Id,
            StartDate   = new DateOnly(2026, 7, 31),
            EndDate     = new DateOnly(2026, 8, 3),
            Reason      = "IT-22 sandwich",
        });
        Assert.True(create.IsSuccess);

        var submit = await svc.SubmitRequestAsync(create.Value!.Id, employee.Id);
        Assert.True(submit.IsSuccess, $"Submit failed: {submit.Error}");
        Assert.Equal(4m, submit.Value!.ComputedDays);
    }

    // ── IT-23 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// IT-23: Submit request 1 (Mon–Wed). Submit overlapping request 2 (Tue–Thu).
    /// Assert second submit returns failure (409 / overlap).
    /// </summary>
    [Fact]
    public async Task IT23_OverlappingRequest_SecondSubmitFails()
    {
        await using var db = CreateDb();
        var svc = BuildSvc(db);
        var (employee, _, leaveType) = SeedAnnualLeave(db, allocated: 20m);

        // First request: Mon 17 Aug – Wed 19 Aug 2026
        var r1 = await svc.CreateRequestAsync(employee.Id, new CreateLeaveRequestDto
        {
            LeaveTypeId = leaveType.Id,
            StartDate   = new DateOnly(2026, 8, 17),
            EndDate     = new DateOnly(2026, 8, 19),
            Reason      = "IT-23 first",
        });
        var submit1 = await svc.SubmitRequestAsync(r1.Value!.Id, employee.Id);
        Assert.True(submit1.IsSuccess, $"First submit failed: {submit1.Error}");

        // Second request: Tue 18 Aug – Thu 20 Aug 2026 — overlaps with first
        var r2 = await svc.CreateRequestAsync(employee.Id, new CreateLeaveRequestDto
        {
            LeaveTypeId = leaveType.Id,
            StartDate   = new DateOnly(2026, 8, 18),
            EndDate     = new DateOnly(2026, 8, 20),
            Reason      = "IT-23 overlap",
        });
        var submit2 = await svc.SubmitRequestAsync(r2.Value!.Id, employee.Id);

        Assert.False(submit2.IsSuccess, "Second overlapping submit should have been rejected");
    }

    // ── IT-24 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// IT-24: Submit leave (2 days), then cancel. Assert balance.UsedDays restored to 0.
    /// </summary>
    [Fact]
    public async Task IT24_SubmitThenCancel_BalanceUsedDaysRestoredToZero()
    {
        await using var db = CreateDb();
        var svc = BuildSvc(db);
        var (employee, _, leaveType) = SeedAnnualLeave(db, allocated: 10m);

        // Mon 24 Aug – Tue 25 Aug 2026 (2 working days)
        var r = await svc.CreateRequestAsync(employee.Id, new CreateLeaveRequestDto
        {
            LeaveTypeId = leaveType.Id,
            StartDate   = new DateOnly(2026, 8, 24),
            EndDate     = new DateOnly(2026, 8, 25),
            Reason      = "IT-24 cancel",
        });
        var submit = await svc.SubmitRequestAsync(r.Value!.Id, employee.Id);
        Assert.True(submit.IsSuccess, $"Submit failed: {submit.Error}");

        var cancel = await svc.CancelRequestAsync(r.Value!.Id, employee.Id);
        Assert.True(cancel.IsSuccess, $"Cancel failed: {cancel.Error}");

        db.ChangeTracker.Clear();
        var bal = await db.LeaveBalances
            .FirstAsync(b => b.UserId == employee.Id && b.LeaveTypeId == leaveType.Id);
        Assert.Equal(0m, bal.UsedDays);
    }

    // ── IT-25 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// IT-25: Seed Unpaid Leave type (AccrualType=Unlimited). Submit with 0 balance.
    /// Assert Result.IsSuccess — no balance check for Unlimited.
    /// </summary>
    [Fact]
    public async Task IT25_UnpaidLeave_ZeroBalance_SubmitSucceeds()
    {
        await using var db = CreateDb();
        var svc = BuildSvc(db);

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
        var unpaidType = new LeaveType
        {
            Id               = Guid.NewGuid(),
            Name             = "Unpaid Leave",
            MaxDaysPerYear   = null,
            AccrualType      = AccrualType.Unlimited, // bypass balance check
            RequiresDocument = false,
            IsActive         = true,
            CreatedAt        = DateTime.UtcNow,
            UpdatedAt        = DateTime.UtcNow,
        };
        db.Users.AddRange(hrAdmin, employee);
        db.LeaveTypes.Add(unpaidType);
        await db.SaveChangesAsync();
        // Intentionally no LeaveBalance row — zero balance.

        var r = await svc.CreateRequestAsync(employee.Id, new CreateLeaveRequestDto
        {
            LeaveTypeId = unpaidType.Id,
            StartDate   = new DateOnly(2026, 9, 1),
            EndDate     = new DateOnly(2026, 9, 2),
            Reason      = "IT-25 unpaid",
        });
        Assert.True(r.IsSuccess);

        var submit = await svc.SubmitRequestAsync(r.Value!.Id, employee.Id);
        Assert.True(submit.IsSuccess, $"Unpaid submit should succeed with zero balance: {submit.Error}");
    }

    // ── IT-26 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// IT-26: Submit with start_date = yesterday. Assert leave_request.IsRetroactive = true.
    /// </summary>
    [Fact]
    public async Task IT26_PastStartDate_IsRetroactiveTrue()
    {
        await using var db = CreateDb();
        var svc = BuildSvc(db);
        var (employee, _, leaveType) = SeedAnnualLeave(db, allocated: 10m);

        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);

        var r = await svc.CreateRequestAsync(employee.Id, new CreateLeaveRequestDto
        {
            LeaveTypeId = leaveType.Id,
            StartDate   = yesterday,
            EndDate     = yesterday,
            Reason      = "IT-26 retroactive",
        });
        Assert.True(r.IsSuccess);

        var submit = await svc.SubmitRequestAsync(r.Value!.Id, employee.Id);
        Assert.True(submit.IsSuccess, $"Submit failed: {submit.Error}");
        Assert.True(submit.Value!.IsRetroactive,
            "Request with past start_date must be flagged IsRetroactive=true");
    }

    // ── IT-27 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// IT-27: Submit leave type with RequiresDocument=true and no document_url.
    /// Assert Result is failure.
    /// </summary>
    [Fact]
    public async Task IT27_RequiresDocument_NoDocumentUrl_SubmitFails()
    {
        await using var db = CreateDb();
        var svc = BuildSvc(db);

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
        var sickType = new LeaveType
        {
            Id               = Guid.NewGuid(),
            Name             = "Sick Leave",
            MaxDaysPerYear   = 10,
            AccrualType      = AccrualType.Annual,
            RequiresDocument = true, // <-- requires a document
            IsActive         = true,
            CreatedAt        = DateTime.UtcNow,
            UpdatedAt        = DateTime.UtcNow,
        };
        db.Users.AddRange(hrAdmin, employee);
        db.LeaveTypes.Add(sickType);
        db.LeaveBalances.Add(new LeaveBalance
        {
            Id            = Guid.NewGuid(),
            UserId        = employee.Id,
            LeaveTypeId   = sickType.Id,
            Year          = (short)DateTime.UtcNow.Year,
            AllocatedDays = 10m,
            UsedDays      = 0m,
            CreatedAt     = DateTime.UtcNow,
            UpdatedAt     = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var r = await svc.CreateRequestAsync(employee.Id, new CreateLeaveRequestDto
        {
            LeaveTypeId = sickType.Id,
            StartDate   = new DateOnly(2026, 9, 7),
            EndDate     = new DateOnly(2026, 9, 7),
            DocumentUrl = null, // missing required document
            Reason      = "IT-27 no doc",
        });
        Assert.True(r.IsSuccess);

        var submit = await svc.SubmitRequestAsync(r.Value!.Id, employee.Id);
        Assert.False(submit.IsSuccess, "Submit must fail when document is required but missing");
        Assert.Contains("document", submit.Error, StringComparison.OrdinalIgnoreCase);
    }

    // ── IT-28 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// IT-28: Submit, simulate approve (set Status=Approved in DB), revoke by HR Admin.
    /// Assert balance.UsedDays restored to 0.
    /// </summary>
    [Fact]
    public async Task IT28_ApprovedRequestRevoked_BalanceRestored()
    {
        await using var db = CreateDb();
        var svc = BuildSvc(db);
        var (employee, hrAdmin, leaveType) = SeedAnnualLeave(db, allocated: 10m);

        // Mon 14 Sep – Tue 15 Sep 2026 (2 working days)
        var r = await svc.CreateRequestAsync(employee.Id, new CreateLeaveRequestDto
        {
            LeaveTypeId = leaveType.Id,
            StartDate   = new DateOnly(2026, 9, 14),
            EndDate     = new DateOnly(2026, 9, 15),
            Reason      = "IT-28 revoke",
        });
        var submit = await svc.SubmitRequestAsync(r.Value!.Id, employee.Id);
        Assert.True(submit.IsSuccess, $"Submit failed: {submit.Error}");

        // Simulate approval — set Status = Approved directly in the DB.
        db.ChangeTracker.Clear();
        var lr = await db.LeaveRequests.FindAsync(r.Value!.Id);
        lr!.Status    = LeaveRequestStatus.Approved;
        lr.UpdatedAt  = DateTime.UtcNow;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        // Revoke by HR Admin
        var revoke = await svc.RevokeRequestAsync(r.Value!.Id, hrAdmin.Id);
        Assert.True(revoke.IsSuccess, $"Revoke failed: {revoke.Error}");
        Assert.Equal("Revoked", revoke.Value!.Status);

        db.ChangeTracker.Clear();
        var bal = await db.LeaveBalances
            .FirstAsync(b => b.UserId == employee.Id && b.LeaveTypeId == leaveType.Id);
        Assert.Equal(0m, bal.UsedDays);
    }

    // ── IT-29 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// IT-29: Single day leave (start = end = working day). Assert computed_days = 1.
    /// </summary>
    [Fact]
    public async Task IT29_SingleWorkingDay_ComputedDays1()
    {
        await using var db = CreateDb();
        var svc = BuildSvc(db);
        var (employee, _, leaveType) = SeedAnnualLeave(db, allocated: 10m);

        // Monday 21 Sep 2026
        var r = await svc.CreateRequestAsync(employee.Id, new CreateLeaveRequestDto
        {
            LeaveTypeId = leaveType.Id,
            StartDate   = new DateOnly(2026, 9, 21),
            EndDate     = new DateOnly(2026, 9, 21),
            Reason      = "IT-29 single day",
        });
        var submit = await svc.SubmitRequestAsync(r.Value!.Id, employee.Id);
        Assert.True(submit.IsSuccess, $"Submit failed: {submit.Error}");
        Assert.Equal(1m, submit.Value!.ComputedDays);
    }

    // ── IT-30 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// IT-30: Single day on a registered holiday.
    /// Assert computed_days = 0 (the holiday has no working-day neighbours within
    /// the single-day range, so it is not sandwiched and not counted).
    /// </summary>
    [Fact]
    public async Task IT30_SingleDayOnHoliday_ComputedDays0()
    {
        await using var db = CreateDb();
        var svc = BuildSvc(db);
        var (employee, _, leaveType) = SeedAnnualLeave(db, allocated: 10m);

        // Register Monday 5 Oct 2026 as a holiday
        var holidayDate = new DateOnly(2026, 10, 5); // Monday
        db.Holidays.Add(new Holiday
        {
            Id          = Guid.NewGuid(),
            Name        = "IT-30 Test Holiday",
            Date        = holidayDate,
            Year        = 2026,
            IsRecurring = false,
            CreatedAt   = DateTime.UtcNow,
            UpdatedAt   = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var r = await svc.CreateRequestAsync(employee.Id, new CreateLeaveRequestDto
        {
            LeaveTypeId = leaveType.Id,
            StartDate   = holidayDate,
            EndDate     = holidayDate,
            Reason      = "IT-30 holiday day",
        });
        var submit = await svc.SubmitRequestAsync(r.Value!.Id, employee.Id);
        Assert.True(submit.IsSuccess, $"Submit failed: {submit.Error}");
        // No working-day neighbours within [holidayDate, holidayDate] — not sandwiched.
        Assert.Equal(0m, submit.Value!.ComputedDays);
    }

    // ── IT-31 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// IT-31: Mon–Fri 12–16 Oct 2026 with Wed 14 Oct registered as a holiday.
    /// The holiday is sandwiched between Tue(working) and Thu(working).
    /// Assert computed_days = 5 (Mon + Tue + Wed[sandwiched] + Thu + Fri).
    /// </summary>
    [Fact]
    public async Task IT31_MondayToFriday_WednesdayHoliday_ComputedDays5()
    {
        await using var db = CreateDb();
        var svc = BuildSvc(db);
        var (employee, _, leaveType) = SeedAnnualLeave(db, allocated: 10m);

        // Register Wednesday 14 Oct 2026 as a holiday
        db.Holidays.Add(new Holiday
        {
            Id          = Guid.NewGuid(),
            Name        = "IT-31 Midweek Holiday",
            Date        = new DateOnly(2026, 10, 14),
            Year        = 2026,
            IsRecurring = false,
            CreatedAt   = DateTime.UtcNow,
            UpdatedAt   = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var r = await svc.CreateRequestAsync(employee.Id, new CreateLeaveRequestDto
        {
            LeaveTypeId = leaveType.Id,
            StartDate   = new DateOnly(2026, 10, 12), // Monday
            EndDate     = new DateOnly(2026, 10, 16), // Friday
            Reason      = "IT-31 sandwich holiday",
        });
        var submit = await svc.SubmitRequestAsync(r.Value!.Id, employee.Id);
        Assert.True(submit.IsSuccess, $"Submit failed: {submit.Error}");
        // Mon(1) + Tue(1) + Wed-holiday(sandwiched, 1) + Thu(1) + Fri(1) = 5
        Assert.Equal(5m, submit.Value!.ComputedDays);
    }

    // ── IT-32 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// IT-32: Full status-machine walk:
    ///   CreateRequestAsync  → Status = Draft
    ///   SubmitRequestAsync  → Status = Pending
    ///   (DB direct)         → Status = Approved
    ///   RevokeRequestAsync  → Status = Revoked
    /// Assert each transition.
    /// </summary>
    [Fact]
    public async Task IT32_StateMachine_Draft_Pending_Approved_Revoked()
    {
        await using var db = CreateDb();
        var svc = BuildSvc(db);
        var (employee, hrAdmin, leaveType) = SeedAnnualLeave(db, allocated: 10m);

        // Draft
        var create = await svc.CreateRequestAsync(employee.Id, new CreateLeaveRequestDto
        {
            LeaveTypeId = leaveType.Id,
            StartDate   = new DateOnly(2026, 10, 19), // Monday
            EndDate     = new DateOnly(2026, 10, 19),
            Reason      = "IT-32 state machine",
        });
        Assert.True(create.IsSuccess);
        Assert.Equal("Draft", create.Value!.Status);

        // Draft → Pending
        var submit = await svc.SubmitRequestAsync(create.Value!.Id, employee.Id);
        Assert.True(submit.IsSuccess, $"Submit failed: {submit.Error}");
        Assert.Equal("Pending", submit.Value!.Status);

        // Pending → Approved (simulate approver action directly in DB)
        db.ChangeTracker.Clear();
        var lr = await db.LeaveRequests.FindAsync(create.Value!.Id);
        lr!.Status   = LeaveRequestStatus.Approved;
        lr.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var approved = await db.LeaveRequests.FindAsync(create.Value!.Id);
        Assert.Equal(LeaveRequestStatus.Approved, approved!.Status);

        // Approved → Revoked
        var revoke = await svc.RevokeRequestAsync(create.Value!.Id, hrAdmin.Id);
        Assert.True(revoke.IsSuccess, $"Revoke failed: {revoke.Error}");
        Assert.Equal("Revoked", revoke.Value!.Status);
    }
}
