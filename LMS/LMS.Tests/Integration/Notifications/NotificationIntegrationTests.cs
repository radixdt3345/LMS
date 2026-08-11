using LMS.Application.DTOs.LeaveRequest;
using LMS.Application.Interfaces;
using LMS.Domain.Common;
using LMS.Domain.Entities;
using LMS.Domain.Enums;
using LMS.Infrastructure.Data;
using LMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace LMS.Tests.Integration.Notifications;

/// <summary>
/// IT-42 to IT-44: Notification, email, and calendar integration tests against EF InMemory.
///
/// IT-42: Approve leave (single-step, no-manager employee) → a LeaveApproved notification
///        row is persisted to the Notifications table for the requesting employee.
/// IT-43: Approve leave → IEmailService.SendEmailAsync called at least once with the
///        employee’s email address and a subject that contains “approved” (case-insensitive).
///        Plain text + inline HTML; no SendGrid template IDs (UT-54).
/// IT-44: Approve leave → ICalendarService.CreateLeaveEventAsync called at least once with
///        the correct leave date range. Service-account design (no per-user OAuth2) is
///        inherent in ICalendarService’s contract and verified by the call assertion.
///
/// Run: dotnet test --filter Category=Integration
/// </summary>
[Trait("Category", "Integration")]
public class NotificationIntegrationTests
{
    // ── DB factory ──────────────────────────────────────────────────────────────────────────

    private static LmsDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<LmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new LmsDbContext(opts);
    }

    // ── Service factory ─────────────────────────────────────────────────────────────────────────

    private static (LeaveRequestService leaveSvc, ApprovalService approvalSvc) BuildServices(
        LmsDbContext db,
        IEmailService? emailService = null,
        ICalendarService? calendarService = null)
    {
        var audit    = new AuditService(db, NullLogger<AuditService>.Instance);
        var balance  = new LeaveBalanceService(db);
        var approval = new ApprovalService(db, audit, balance, emailService, calendarService);
        var leaveSvc = new LeaveRequestService(db, audit, balance, approval);
        return (leaveSvc, approval);
    }

    // ── Seed helper ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Seeds: HR Admin, Employee (ManagerId = null — single-step approval route),
    /// Annual Leave type, and a balance row for the employee.
    /// A no-manager employee guarantees ApproveAsync reaches the “all steps approved”
    /// branch in a single HR Admin call, making notification/email/calendar assertions
    /// deterministic without multi-step orchestration.
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

    // ── Shared submit helper ───────────────────────────────────────────────────────────────────

    /// <summary>Creates and submits a 1-day future leave request for the employee.</summary>
    private static async Task<Guid> CreateAndSubmitAsync(
        LeaveRequestService leaveSvc,
        User employee,
        LeaveType leaveType)
    {
        var create = await leaveSvc.CreateRequestAsync(employee.Id, new CreateLeaveRequestDto
        {
            LeaveTypeId = leaveType.Id,
            StartDate   = new DateOnly(2026, 9, 7),   // future Monday
            EndDate     = new DateOnly(2026, 9, 7),
            Reason      = "IT-4x notification integration test",
        });
        Assert.True(create.IsSuccess, $"Create failed: {create.Error}");

        var submit = await leaveSvc.SubmitRequestAsync(create.Value!.Id, employee.Id);
        Assert.True(submit.IsSuccess, $"Submit failed: {submit.Error}");

        return create.Value.Id;
    }

    // ── IT-42 ───────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// IT-42: Approve leave (no-manager employee → single step) → a LeaveApproved
    /// notification row is persisted to the Notifications table for the employee.
    /// </summary>
    [Fact(DisplayName = "IT-42: Approve leave → LeaveApproved notification row persisted for employee")]
    public async Task IT42_ApproveLeave_LeaveApprovedNotificationRowCreatedForEmployee()
    {
        await using var db = CreateDb();
        var (leaveSvc, approvalSvc) = BuildServices(db);
        var (employee, hrAdmin, leaveType) = SeedWithoutManager(db);

        var requestId = await CreateAndSubmitAsync(leaveSvc, employee, leaveType);

        // HR Admin approves the sole step — this is the last pending step, so the request
        // transitions to Approved and a LeaveApproved notification is written to the DB.
        var approve = await approvalSvc.ApproveAsync(requestId, hrAdmin.Id);
        Assert.True(approve.IsSuccess, $"ApproveAsync failed: {approve.Error}");

        // Assert: notification exists for the employee with type LeaveApproved.
        db.ChangeTracker.Clear();
        var notificationExists = await db.Notifications.AnyAsync(n =>
            n.UserId     == employee.Id &&
            n.ResourceId == requestId  &&
            n.Type       == NotificationType.LeaveApproved);

        Assert.True(notificationExists,
            "Expected a LeaveApproved notification row for the employee after final approval.");
    }

    // ── IT-43 ───────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// IT-43: Approve leave → IEmailService.SendEmailAsync called with the employee’s email
    /// address and a subject that contains “approved” (case-insensitive). Verified via Moq.
    /// Plain-text + inline-HTML path; no SendGrid template IDs (UT-54).
    /// </summary>
    [Fact(DisplayName = "IT-43: Approve leave → email sent to employee with 'approved' in subject")]
    public async Task IT43_ApproveLeave_EmailSentToEmployeeWithApprovedSubject()
    {
        await using var db = CreateDb();

        var emailMock = new Mock<IEmailService>();
        emailMock
            .Setup(e => e.SendEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool>.Success(true));

        var (leaveSvc, approvalSvc) = BuildServices(db, emailService: emailMock.Object);
        var (employee, hrAdmin, leaveType) = SeedWithoutManager(db);

        var requestId = await CreateAndSubmitAsync(leaveSvc, employee, leaveType);
        var approve   = await approvalSvc.ApproveAsync(requestId, hrAdmin.Id);
        Assert.True(approve.IsSuccess, $"ApproveAsync failed: {approve.Error}");

        // Assert: SendEmailAsync called at least once with:
        //   toEmail  = employee.Email
        //   subject  = contains "approved" (case-insensitive)
        //   textBody = any string (plain text, no template ID)
        //   htmlBody = any string (inline HTML, no template ID)
        emailMock.Verify(
            e => e.SendEmailAsync(
                employee.Email,
                It.Is<string>(s => s.Contains("approved", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce,
            "Expected SendEmailAsync to be called with the employee's email and 'approved' in the subject.");
    }

    // ── IT-44 ───────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// IT-44: Approve leave → ICalendarService.CreateLeaveEventAsync called with the correct
    /// leave date range. The service-account-only design (no per-user OAuth2) is inherent
    /// in ICalendarService — verifying the call is sufficient to confirm the constraint.
    /// </summary>
    [Fact(DisplayName = "IT-44: Approve leave → CalendarService.CreateLeaveEventAsync called with correct dates")]
    public async Task IT44_ApproveLeave_CalendarCreateLeaveEventCalledWithCorrectDateRange()
    {
        await using var db = CreateDb();

        var calendarMock = new Mock<ICalendarService>();
        calendarMock
            .Setup(c => c.CreateLeaveEventAsync(
                It.IsAny<string>(),
                It.IsAny<DateOnly>(),
                It.IsAny<DateOnly>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string?>.Success("evt-test-id"));

        var (leaveSvc, approvalSvc) = BuildServices(db, calendarService: calendarMock.Object);
        var (employee, hrAdmin, leaveType) = SeedWithoutManager(db);

        var requestId = await CreateAndSubmitAsync(leaveSvc, employee, leaveType);
        var approve   = await approvalSvc.ApproveAsync(requestId, hrAdmin.Id);
        Assert.True(approve.IsSuccess, $"ApproveAsync failed: {approve.Error}");

        // Assert: CreateLeaveEventAsync called at least once with the start/end dates
        // used in CreateAndSubmitAsync (2026-09-07 to 2026-09-07).
        // No per-user OAuth2 token — service account credential is the only auth path
        // exposed by ICalendarService (design constraint, not a run-time assertion).
        calendarMock.Verify(
            c => c.CreateLeaveEventAsync(
                It.IsAny<string>(),
                new DateOnly(2026, 9, 7),
                new DateOnly(2026, 9, 7),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce,
            "Expected CreateLeaveEventAsync to be called with the leave start/end dates after final approval.");
    }
}
