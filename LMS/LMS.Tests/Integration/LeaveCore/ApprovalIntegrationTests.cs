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
/// IT-37 to IT-41: ApprovalService integration tests against EF Core InMemory.
///
/// IT-37: Employee with manager, non-retroactive leave.
///        ApprovalService.CreateApprovalStepsAsync routes purely by ManagerId — two steps are
///        always created when ManagerId is set (L1=direct manager, L2=HR Admin).
///        Manager approves L1 → request stays Pending. HR Admin approves L2 → Approved.
///        Balance remains deducted (deducted at submit time, preserved on approval).
/// IT-38: Employee with manager_id=NULL → HR Admin is sole approver (L2 unconditionally skipped).
///        HR Admin approves → Approved, exactly 1 approval_step row.
/// IT-39: Employee with manager, retroactive leave (start_date = yesterday).
///        2 approval_step rows created. Manager approves L1 → request stays Pending.
///        HR Admin approves L2 → Approved.
/// IT-40 (CRITICAL — no-manager rule): Employee with manager_id=NULL, retroactive leave.
///        L2 is unconditionally skipped even for retroactive requests (UT-53).
///        HR Admin approves the sole step → Approved, exactly 1 step row.
/// IT-41: Employee with manager. Manager rejects at step 1 with a comment.
///        leave_request.Status=Rejected, balance restored to 0, step.Status=Rejected.
///
/// Run: dotnet test --filter Category=Integration
/// </summary>
[Trait("Category", "Integration")]
public class ApprovalIntegrationTests
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

    private static (LeaveRequestService leaveSvc, ApprovalService approvalSvc) BuildServices(LmsDbContext db)
    {
        var audit    = new AuditService(db, NullLogger<AuditService>.Instance);
        var balance  = new LeaveBalanceService(db);
        var approval = new ApprovalService(db, audit, balance);
        var leaveSvc = new LeaveRequestService(db, audit, balance, approval);
        return (leaveSvc, approval);
    }

    // ── Seed helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Seeds: HR Admin, Manager, Employee (ManagerId = manager.Id),
    /// Annual Leave type, and a balance row for the employee.
    /// </summary>
    private static (User employee, User manager, User hrAdmin, LeaveType leaveType)
        SeedWithManager(LmsDbContext db, decimal allocated = 10m)
    {
        var hrAdmin = new User
        {
            Id        = Guid.NewGuid(),
            Email     = $"hr-{Guid.NewGuid():N}@test.com",
            Role      = UserRole.HRAdmin,
            IsActive  = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        var manager = new User
        {
            Id        = Guid.NewGuid(),
            Email     = $"mgr-{Guid.NewGuid():N}@test.com",
            Role      = UserRole.Manager,
            IsActive  = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        var employee = new User
        {
            Id        = Guid.NewGuid(),
            Email     = $"emp-{Guid.NewGuid():N}@test.com",
            Role      = UserRole.Employee,
            IsActive  = true,
            ManagerId = manager.Id,
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
        db.Users.AddRange(hrAdmin, manager, employee);
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
        return (employee, manager, hrAdmin, leaveType);
    }

    /// <summary>
    /// Seeds: HR Admin, Employee (ManagerId = null),
    /// Annual Leave type, and a balance row for the employee.
    /// </summary>
    private static (User employee, User hrAdmin, LeaveType leaveType)
        SeedWithoutManager(LmsDbContext db, decimal allocated = 10m)
    {
        var hrAdmin = new User
        {
            Id        = Guid.NewGuid(),
            Email     = $"hr-{Guid.NewGuid():N}@test.com",
            Role      = UserRole.HRAdmin,
            IsActive  = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        var employee = new User
        {
            Id        = Guid.NewGuid(),
            Email     = $"emp-{Guid.NewGuid():N}@test.com",
            Role      = UserRole.Employee,
            IsActive  = true,
            ManagerId = null,
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

    // ── IT-37 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// IT-37: Employee with manager, non-retroactive leave.
    /// CreateApprovalStepsAsync creates 2 steps (L1=manager, L2=HR Admin).
    /// Manager approves step 1 → request stays Pending.
    /// HR Admin approves step 2 → request Approved.
    /// Balance deducted at submit time remains deducted after full approval.
    /// </summary>
    [Fact(DisplayName = "IT-37: Employee with manager → 2-step approval (L1+L2) → Approved, balance deducted")]
    public async Task IT37_EmployeeWithManager_NonRetroactive_FullTwoStepApproval_RequestApproved_BalanceDeducted()
    {
        await using var db = CreateDb();
        var (leaveSvc, approvalSvc) = BuildServices(db);
        var (employee, manager, hrAdmin, leaveType) = SeedWithManager(db, allocated: 10m);

        // Submit non-retroactive leave — Mon 10 Aug 2026 (1 working day)
        var create = await leaveSvc.CreateRequestAsync(employee.Id, new CreateLeaveRequestDto
        {
            LeaveTypeId = leaveType.Id,
            StartDate   = new DateOnly(2026, 8, 10),
            EndDate     = new DateOnly(2026, 8, 10),
            Reason      = "IT-37 non-retroactive",
        });
        Assert.True(create.IsSuccess, $"Create failed: {create.Error}");

        var submit = await leaveSvc.SubmitRequestAsync(create.Value!.Id, employee.Id);
        Assert.True(submit.IsSuccess, $"Submit failed: {submit.Error}");
        Assert.False(submit.Value!.IsRetroactive, "Expected non-retroactive for future date");

        // Two approval steps created: StepNumber 1 = manager (L1), StepNumber 2 = HR Admin (L2)
        db.ChangeTracker.Clear();
        var steps = await db.ApprovalSteps
            .Where(s => s.LeaveRequestId == create.Value.Id)
            .OrderBy(s => s.StepNumber)
            .ToListAsync();
        Assert.Equal(2, steps.Count);
        Assert.Equal(manager.Id, steps[0].ApproverId);
        Assert.Equal(hrAdmin.Id, steps[1].ApproverId);
        Assert.All(steps, s => Assert.Equal(ApprovalStepStatus.Pending, s.Status));

        // Manager approves step 1 — request must remain Pending (L2 still outstanding)
        var approve1 = await approvalSvc.ApproveAsync(create.Value.Id, manager.Id);
        Assert.True(approve1.IsSuccess, $"Manager approval failed: {approve1.Error}");

        db.ChangeTracker.Clear();
        var reqAfterL1 = await db.LeaveRequests.FindAsync(create.Value.Id);
        Assert.Equal(LeaveRequestStatus.Pending, reqAfterL1!.Status);

        // HR Admin approves step 2 — request must now be Approved
        var approve2 = await approvalSvc.ApproveAsync(create.Value.Id, hrAdmin.Id);
        Assert.True(approve2.IsSuccess, $"HR Admin approval failed: {approve2.Error}");

        db.ChangeTracker.Clear();
        var reqApproved = await db.LeaveRequests.FindAsync(create.Value.Id);
        Assert.Equal(LeaveRequestStatus.Approved, reqApproved!.Status);

        // Both steps are now Approved
        db.ChangeTracker.Clear();
        var finalSteps = await db.ApprovalSteps
            .Where(s => s.LeaveRequestId == create.Value.Id)
            .ToListAsync();
        Assert.All(finalSteps, s => Assert.Equal(ApprovalStepStatus.Approved, s.Status));

        // Balance remains deducted (1 day — deducted at submit, not restored on approval)
        db.ChangeTracker.Clear();
        var bal = await db.LeaveBalances
            .FirstAsync(b => b.UserId == employee.Id && b.LeaveTypeId == leaveType.Id);
        Assert.Equal(1m, bal.UsedDays);
    }

    // ── IT-38 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// IT-38: Employee with manager_id=NULL. HR Admin is sole approver (L2 unconditionally skipped).
    /// HR Admin approves the single step → request Approved, exactly 1 approval_step row.
    /// </summary>
    [Fact(DisplayName = "IT-38: Employee without manager → HR Admin sole approver → Approved (1 step, L2 skipped)")]
    public async Task IT38_EmployeeNoManager_HRAdminSoleApprover_RequestApprovedWithOneStep()
    {
        await using var db = CreateDb();
        var (leaveSvc, approvalSvc) = BuildServices(db);
        var (employee, hrAdmin, leaveType) = SeedWithoutManager(db, allocated: 10m);

        var create = await leaveSvc.CreateRequestAsync(employee.Id, new CreateLeaveRequestDto
        {
            LeaveTypeId = leaveType.Id,
            StartDate   = new DateOnly(2026, 8, 10),
            EndDate     = new DateOnly(2026, 8, 10),
            Reason      = "IT-38 no-manager",
        });
        Assert.True(create.IsSuccess, $"Create failed: {create.Error}");

        var submit = await leaveSvc.SubmitRequestAsync(create.Value!.Id, employee.Id);
        Assert.True(submit.IsSuccess, $"Submit failed: {submit.Error}");

        // Exactly 1 approval step — HR Admin is L1; L2 is skipped
        db.ChangeTracker.Clear();
        var steps = await db.ApprovalSteps
            .Where(s => s.LeaveRequestId == create.Value.Id)
            .ToListAsync();
        Assert.Single(steps);
        Assert.Equal(hrAdmin.Id, steps[0].ApproverId);
        Assert.Equal(ApprovalStepStatus.Pending, steps[0].Status);

        // HR Admin approves the sole step → request Approved in one action
        var approve = await approvalSvc.ApproveAsync(create.Value.Id, hrAdmin.Id);
        Assert.True(approve.IsSuccess, $"HR Admin approval failed: {approve.Error}");

        db.ChangeTracker.Clear();
        var req = await db.LeaveRequests.FindAsync(create.Value.Id);
        Assert.Equal(LeaveRequestStatus.Approved, req!.Status);

        // Still exactly 1 step, now Approved
        db.ChangeTracker.Clear();
        var finalStep = await db.ApprovalSteps
            .SingleAsync(s => s.LeaveRequestId == create.Value.Id);
        Assert.Equal(ApprovalStepStatus.Approved, finalStep.Status);
    }

    // ── IT-39 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// IT-39: Employee with manager, retroactive leave (start_date = yesterday).
    /// 2 approval_step rows are created. Manager approves step 1 → request stays Pending.
    /// HR Admin approves step 2 → request Approved.
    /// </summary>
    [Fact(DisplayName = "IT-39: Retroactive leave with manager → 2 steps; L1 approve → Pending; L2 approve → Approved")]
    public async Task IT39_RetroactiveLeave_WithManager_TwoStepFlow_ManagerThenHRAdmin_Approved()
    {
        await using var db = CreateDb();
        var (leaveSvc, approvalSvc) = BuildServices(db);
        var (employee, manager, hrAdmin, leaveType) = SeedWithManager(db, allocated: 10m);

        // Retroactive: start_date is in the past
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);

        var create = await leaveSvc.CreateRequestAsync(employee.Id, new CreateLeaveRequestDto
        {
            LeaveTypeId = leaveType.Id,
            StartDate   = yesterday,
            EndDate     = yesterday,
            Reason      = "IT-39 retroactive",
        });
        Assert.True(create.IsSuccess, $"Create failed: {create.Error}");

        var submit = await leaveSvc.SubmitRequestAsync(create.Value!.Id, employee.Id);
        Assert.True(submit.IsSuccess, $"Submit failed: {submit.Error}");
        Assert.True(submit.Value!.IsRetroactive,
            "Request with past start_date must be flagged IsRetroactive=true");

        // Verify 2 approval steps exist
        db.ChangeTracker.Clear();
        var steps = await db.ApprovalSteps
            .Where(s => s.LeaveRequestId == create.Value.Id)
            .OrderBy(s => s.StepNumber)
            .ToListAsync();
        Assert.Equal(2, steps.Count);
        Assert.Equal(manager.Id, steps[0].ApproverId);
        Assert.Equal(hrAdmin.Id, steps[1].ApproverId);

        // Manager approves step 1 → request stays Pending (step 2 still outstanding)
        var approve1 = await approvalSvc.ApproveAsync(create.Value.Id, manager.Id);
        Assert.True(approve1.IsSuccess, $"Manager approval failed: {approve1.Error}");

        db.ChangeTracker.Clear();
        var reqAfterStep1 = await db.LeaveRequests.FindAsync(create.Value.Id);
        Assert.Equal(LeaveRequestStatus.Pending, reqAfterStep1!.Status);

        // HR Admin approves step 2 → request Approved
        var approve2 = await approvalSvc.ApproveAsync(create.Value.Id, hrAdmin.Id);
        Assert.True(approve2.IsSuccess, $"HR Admin approval failed: {approve2.Error}");

        db.ChangeTracker.Clear();
        var reqAfterStep2 = await db.LeaveRequests.FindAsync(create.Value.Id);
        Assert.Equal(LeaveRequestStatus.Approved, reqAfterStep2!.Status);
    }

    // ── IT-40 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// IT-40 (CRITICAL — no-manager rule): Employee with manager_id=NULL, retroactive leave.
    /// L2 is unconditionally skipped even for retroactive requests (UT-53, IT-40).
    /// HR Admin approves the sole step → Approved, exactly 1 approval_step row.
    /// </summary>
    [Fact(DisplayName = "IT-40 (CRITICAL): No-manager rule — retroactive request routes to HR Admin only (1 step, L2 skipped)")]
    public async Task IT40_RetroactiveLeave_NoManager_L2UnconditionallySkipped_OneStepApproval()
    {
        await using var db = CreateDb();
        var (leaveSvc, approvalSvc) = BuildServices(db);
        var (employee, hrAdmin, leaveType) = SeedWithoutManager(db, allocated: 10m);

        // Retroactive: start_date = yesterday
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);

        var create = await leaveSvc.CreateRequestAsync(employee.Id, new CreateLeaveRequestDto
        {
            LeaveTypeId = leaveType.Id,
            StartDate   = yesterday,
            EndDate     = yesterday,
            Reason      = "IT-40 retroactive no-manager",
        });
        Assert.True(create.IsSuccess, $"Create failed: {create.Error}");

        var submit = await leaveSvc.SubmitRequestAsync(create.Value!.Id, employee.Id);
        Assert.True(submit.IsSuccess, $"Submit failed: {submit.Error}");
        Assert.True(submit.Value!.IsRetroactive,
            "Request with past start_date must be flagged IsRetroactive=true");

        // CRITICAL: employee.ManagerId IS NULL → exactly 1 step even for retroactive requests
        db.ChangeTracker.Clear();
        var steps = await db.ApprovalSteps
            .Where(s => s.LeaveRequestId == create.Value.Id)
            .ToListAsync();
        Assert.Single(steps);
        Assert.Equal(hrAdmin.Id, steps[0].ApproverId);

        // HR Admin approves the sole step → request Approved
        var approve = await approvalSvc.ApproveAsync(create.Value.Id, hrAdmin.Id);
        Assert.True(approve.IsSuccess, $"HR Admin approval failed: {approve.Error}");

        db.ChangeTracker.Clear();
        var req = await db.LeaveRequests.FindAsync(create.Value.Id);
        Assert.Equal(LeaveRequestStatus.Approved, req!.Status);

        // Confirm exactly 1 approval_step row — L2 was never created
        db.ChangeTracker.Clear();
        var stepCount = await db.ApprovalSteps
            .CountAsync(s => s.LeaveRequestId == create.Value.Id);
        Assert.Equal(1, stepCount);
    }

    // ── IT-41 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// IT-41: Employee with manager. Manager rejects at step 1 with a comment.
    /// leave_request.Status=Rejected, balance restored to 0, step.Status=Rejected.
    /// </summary>
    [Fact(DisplayName = "IT-41: Manager rejects leave → Status=Rejected, balance restored, step.Status=Rejected")]
    public async Task IT41_ManagerRejects_LeaveRejected_BalanceRestored_StepRejectedWithComment()
    {
        await using var db = CreateDb();
        var (leaveSvc, approvalSvc) = BuildServices(db);
        var (employee, manager, hrAdmin, leaveType) = SeedWithManager(db, allocated: 10m);

        // Submit 2-working-day leave (Mon 10 Aug – Tue 11 Aug 2026)
        var create = await leaveSvc.CreateRequestAsync(employee.Id, new CreateLeaveRequestDto
        {
            LeaveTypeId = leaveType.Id,
            StartDate   = new DateOnly(2026, 8, 10),
            EndDate     = new DateOnly(2026, 8, 11),
            Reason      = "IT-41 to be rejected",
        });
        Assert.True(create.IsSuccess, $"Create failed: {create.Error}");

        var submit = await leaveSvc.SubmitRequestAsync(create.Value!.Id, employee.Id);
        Assert.True(submit.IsSuccess, $"Submit failed: {submit.Error}");
        Assert.Equal(2m, submit.Value!.ComputedDays);

        // Verify balance deducted at submit time (2 days)
        db.ChangeTracker.Clear();
        var balBefore = await db.LeaveBalances
            .FirstAsync(b => b.UserId == employee.Id && b.LeaveTypeId == leaveType.Id);
        Assert.Equal(2m, balBefore.UsedDays);

        // Manager rejects at step 1
        const string rejectComment = "Insufficient notice period.";
        var reject = await approvalSvc.RejectAsync(create.Value.Id, manager.Id, rejectComment);
        Assert.True(reject.IsSuccess, $"Reject failed: {reject.Error}");

        // leave_request.Status = Rejected
        db.ChangeTracker.Clear();
        var req = await db.LeaveRequests.FindAsync(create.Value.Id);
        Assert.Equal(LeaveRequestStatus.Rejected, req!.Status);

        // Balance restored — UsedDays back to 0
        db.ChangeTracker.Clear();
        var balAfter = await db.LeaveBalances
            .FirstAsync(b => b.UserId == employee.Id && b.LeaveTypeId == leaveType.Id);
        Assert.Equal(0m, balAfter.UsedDays);

        // Step 1 is Rejected with the comment
        db.ChangeTracker.Clear();
        var step1 = await db.ApprovalSteps
            .FirstAsync(s => s.LeaveRequestId == create.Value.Id && s.StepNumber == 1);
        Assert.Equal(ApprovalStepStatus.Rejected, step1.Status);
        Assert.Equal(rejectComment, step1.Comment);
        Assert.NotNull(step1.ActedAt);
    }
}
