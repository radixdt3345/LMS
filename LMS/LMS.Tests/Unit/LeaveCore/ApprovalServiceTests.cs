using LMS.Application.Interfaces;
using LMS.Domain.Entities;
using LMS.Domain.Enums;
using LMS.Infrastructure.Data;
using LMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace LMS.Tests.Unit.LeaveCore;

[Trait("Category", "Unit")]
public class ApprovalServiceTests : IDisposable
{
    private readonly LmsDbContext _db;
    private readonly Mock<IAuditService> _auditMock;
    private readonly Mock<ILeaveBalanceService> _balanceMock;
    private readonly ApprovalService _sut;

    public ApprovalServiceTests()
    {
        var opts = new DbContextOptionsBuilder<LmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db          = new LmsDbContext(opts);
        _auditMock   = new Mock<IAuditService>();
        _balanceMock = new Mock<ILeaveBalanceService>();
        _sut         = new ApprovalService(_db, _auditMock.Object, _balanceMock.Object);
    }

    public void Dispose() => _db.Dispose();

    private static User MakeUser(UserRole role, Guid? managerId = null) => new()
    {
        Id           = Guid.NewGuid(),
        Email        = $"{Guid.NewGuid()}@lms.test",
        PasswordHash = "hash",
        FirstName    = "Test",
        LastName     = "User",
        Role         = role,
        ManagerId    = managerId,
        IsActive     = true,
        CreatedAt    = DateTime.UtcNow,
        UpdatedAt    = DateTime.UtcNow,
    };

    private static LeaveType MakeLeaveType() => new()
    {
        Id        = Guid.NewGuid(),
        Name      = "Annual Leave",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    private static LeaveRequest MakeRequest(
        Guid employeeId,
        Guid leaveTypeId,
        LeaveRequestStatus status      = LeaveRequestStatus.Pending,
        decimal            computedDays = 3m) => new()
    {
        Id           = Guid.NewGuid(),
        EmployeeId   = employeeId,
        LeaveTypeId  = leaveTypeId,
        StartDate    = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(1)),
        EndDate      = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(3)),
        ComputedDays = computedDays,
        Status       = status,
        Reason       = "unit-test",
        CreatedAt    = DateTime.UtcNow,
        UpdatedAt    = DateTime.UtcNow,
    };

    private static ApprovalStep MakeStep(
        Guid                requestId,
        short               stepNumber,
        Guid                approverId,
        ApprovalStepStatus  status = ApprovalStepStatus.Pending) => new()
    {
        Id             = Guid.NewGuid(),
        LeaveRequestId = requestId,
        StepNumber     = stepNumber,
        ApproverId     = approverId,
        Status         = status,
        CreatedAt      = DateTime.UtcNow,
    };

    [Fact(DisplayName = "UT-48: Manager present → 2 steps: L1=manager, L2=HRAdmin")]
    public async Task CreateApprovalSteps_ManagerPresent_Creates2StepsManagerThenHRAdmin()
    {
        var hrAdmin  = MakeUser(UserRole.HRAdmin);
        var manager  = MakeUser(UserRole.Manager);
        var employee = MakeUser(UserRole.Employee, managerId: manager.Id);
        _db.Users.AddRange(hrAdmin, manager, employee);
        await _db.SaveChangesAsync();

        var requestId = Guid.NewGuid();

        var result = await _sut.CreateApprovalStepsAsync(requestId, employee);

        Assert.True(result.IsSuccess);
        var steps = await _db.ApprovalSteps
            .Where(s => s.LeaveRequestId == requestId)
            .OrderBy(s => s.StepNumber)
            .ToListAsync();
        Assert.Equal(2, steps.Count);
        Assert.Equal(manager.Id,  steps[0].ApproverId);
        Assert.Equal(1,           steps[0].StepNumber);
        Assert.Equal(hrAdmin.Id,  steps[1].ApproverId);
        Assert.Equal(2,           steps[1].StepNumber);
        Assert.All(steps, s => Assert.Equal(ApprovalStepStatus.Pending, s.Status));
    }

    [Fact(DisplayName = "UT-49: No manager → single step, HRAdmin is L1")]
    public async Task CreateApprovalSteps_NoManager_Creates1StepWithHRAdmin()
    {
        var hrAdmin  = MakeUser(UserRole.HRAdmin);
        var employee = MakeUser(UserRole.Employee, managerId: null);
        _db.Users.AddRange(hrAdmin, employee);
        await _db.SaveChangesAsync();

        var requestId = Guid.NewGuid();

        var result = await _sut.CreateApprovalStepsAsync(requestId, employee);

        Assert.True(result.IsSuccess);
        var steps = await _db.ApprovalSteps
            .Where(s => s.LeaveRequestId == requestId)
            .ToListAsync();
        Assert.Single(steps);
        Assert.Equal(1,           steps[0].StepNumber);
        Assert.Equal(hrAdmin.Id,  steps[0].ApproverId);
        Assert.Equal(ApprovalStepStatus.Pending, steps[0].Status);
    }

    [Fact(DisplayName = "UT-50: L1 reject → Status=Rejected, RestoreBalance called")]
    public async Task Reject_L1Rejects_StatusRejectedAndBalanceRestored()
    {
        var manager   = MakeUser(UserRole.Manager);
        var hrAdmin   = MakeUser(UserRole.HRAdmin);
        var employee  = MakeUser(UserRole.Employee, managerId: manager.Id);
        var leaveType = MakeLeaveType();
        _db.Users.AddRange(manager, hrAdmin, employee);
        _db.LeaveTypes.Add(leaveType);
        await _db.SaveChangesAsync();

        var request = MakeRequest(employee.Id, leaveType.Id, computedDays: 3m);
        var stepL1  = MakeStep(request.Id, 1, manager.Id);
        var stepL2  = MakeStep(request.Id, 2, hrAdmin.Id);
        _db.LeaveRequests.Add(request);
        _db.ApprovalSteps.AddRange(stepL1, stepL2);
        await _db.SaveChangesAsync();

        _balanceMock
            .Setup(b => b.RestoreBalance(employee.Id, leaveType.Id, 3m))
            .Returns(Task.CompletedTask);

        var result = await _sut.RejectAsync(request.Id, manager.Id, "Too many days requested");

        Assert.True(result.IsSuccess);
        var updated = await _db.LeaveRequests.FindAsync(request.Id);
        Assert.Equal(LeaveRequestStatus.Rejected, updated!.Status);
        _balanceMock.Verify(
            b => b.RestoreBalance(employee.Id, leaveType.Id, 3m),
            Times.Once);
    }

    [Fact(DisplayName = "UT-51: L2 reject (after L1 approved) → Status=Rejected, RestoreBalance called")]
    public async Task Reject_L2RejectsAfterL1Approved_StatusRejectedAndBalanceRestored()
    {
        var manager   = MakeUser(UserRole.Manager);
        var hrAdmin   = MakeUser(UserRole.HRAdmin);
        var employee  = MakeUser(UserRole.Employee, managerId: manager.Id);
        var leaveType = MakeLeaveType();
        _db.Users.AddRange(manager, hrAdmin, employee);
        _db.LeaveTypes.Add(leaveType);
        await _db.SaveChangesAsync();

        var request = MakeRequest(employee.Id, leaveType.Id, computedDays: 2m);
        var stepL1  = MakeStep(request.Id, 1, manager.Id, ApprovalStepStatus.Approved);
        var stepL2  = MakeStep(request.Id, 2, hrAdmin.Id, ApprovalStepStatus.Pending);
        _db.LeaveRequests.Add(request);
        _db.ApprovalSteps.AddRange(stepL1, stepL2);
        await _db.SaveChangesAsync();

        _balanceMock
            .Setup(b => b.RestoreBalance(employee.Id, leaveType.Id, 2m))
            .Returns(Task.CompletedTask);

        var result = await _sut.RejectAsync(request.Id, hrAdmin.Id, "Policy violation");

        Assert.True(result.IsSuccess);
        var updated = await _db.LeaveRequests.FindAsync(request.Id);
        Assert.Equal(LeaveRequestStatus.Rejected, updated!.Status);
        _balanceMock.Verify(
            b => b.RestoreBalance(employee.Id, leaveType.Id, 2m),
            Times.Once);
    }

    [Fact(DisplayName = "UT-52: L1 approve (2-step) → Status still Pending, L2 notified in-app")]
    public async Task Approve_L1In2StepFlow_RequestStaysPendingAndL2Notified()
    {
        var manager   = MakeUser(UserRole.Manager);
        var hrAdmin   = MakeUser(UserRole.HRAdmin);
        var employee  = MakeUser(UserRole.Employee, managerId: manager.Id);
        var leaveType = MakeLeaveType();
        _db.Users.AddRange(manager, hrAdmin, employee);
        _db.LeaveTypes.Add(leaveType);
        await _db.SaveChangesAsync();

        var request = MakeRequest(employee.Id, leaveType.Id);
        var stepL1  = MakeStep(request.Id, 1, manager.Id);
        var stepL2  = MakeStep(request.Id, 2, hrAdmin.Id);
        _db.LeaveRequests.Add(request);
        _db.ApprovalSteps.AddRange(stepL1, stepL2);
        await _db.SaveChangesAsync();

        var result = await _sut.ApproveAsync(request.Id, manager.Id);

        Assert.True(result.IsSuccess);

        var updated = await _db.LeaveRequests.FindAsync(request.Id);
        Assert.Equal(LeaveRequestStatus.Pending, updated!.Status);

        var l1 = await _db.ApprovalSteps.FindAsync(stepL1.Id);
        Assert.Equal(ApprovalStepStatus.Approved, l1!.Status);

        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.UserId == hrAdmin.Id && n.ResourceId == request.Id);
        Assert.NotNull(notification);
        Assert.Equal(NotificationType.LeaveSubmitted, notification.Type);
    }

    [Fact(DisplayName = "UT-53: No-manager employee + retroactive → exactly 1 step, L2 unconditionally skipped")]
    public async Task CreateApprovalSteps_NoManagerRetroactive_Creates1StepL2NeverAdded()
    {
        var hrAdmin  = MakeUser(UserRole.HRAdmin);
        var employee = MakeUser(UserRole.Employee, managerId: null);
        _db.Users.AddRange(hrAdmin, employee);
        await _db.SaveChangesAsync();

        var requestId = Guid.NewGuid();
        var retroRequest = new LeaveRequest
        {
            Id           = requestId,
            EmployeeId   = employee.Id,
            LeaveTypeId  = Guid.NewGuid(),
            StartDate    = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-5)),
            EndDate      = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-1)),
            ComputedDays = 5m,
            Status       = LeaveRequestStatus.Pending,
            Reason       = "retroactive unit-test",
            CreatedAt    = DateTime.UtcNow,
            UpdatedAt    = DateTime.UtcNow,
        };
        _db.LeaveRequests.Add(retroRequest);
        await _db.SaveChangesAsync();

        var result = await _sut.CreateApprovalStepsAsync(requestId, employee);

        Assert.True(result.IsSuccess);
        var steps = await _db.ApprovalSteps
            .Where(s => s.LeaveRequestId == requestId)
            .OrderBy(s => s.StepNumber)
            .ToListAsync();

        Assert.Single(steps);
        Assert.Equal(1,                          steps[0].StepNumber);
        Assert.Equal(hrAdmin.Id,                 steps[0].ApproverId);
        Assert.Equal(ApprovalStepStatus.Pending, steps[0].Status);
    }
}
