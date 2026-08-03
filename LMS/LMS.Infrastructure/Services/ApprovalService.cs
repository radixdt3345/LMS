using LMS.Application.DTOs.LeaveRequest;
using LMS.Application.Interfaces;
using LMS.Domain.Common;
using LMS.Domain.Entities;
using LMS.Domain.Enums;
using LMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LMS.Infrastructure.Services;

/// <summary>
/// Implements approval step routing and action processing for leave requests.
///
/// No-manager rule (UT-53, IT-40):
/// - manager_id IS NOT NULL → Step 1 = manager, Step 2 = HR Admin.
/// - manager_id IS NULL     → Step 1 = HR Admin ONLY; L2 unconditionally skipped.
/// </summary>
public class ApprovalService : IApprovalService
{
    private readonly LmsDbContext _db;
    private readonly IAuditService _audit;
    private readonly ILeaveBalanceService _balance;
    private readonly IEmailService? _email;
    private readonly ICalendarService? _calendar;

    public ApprovalService(
        LmsDbContext db,
        IAuditService audit,
        ILeaveBalanceService balance,
        IEmailService? email = null,
        ICalendarService? calendar = null)
    {
        _db       = db;
        _audit    = audit;
        _balance  = balance;
        _email    = email;
        _calendar = calendar;
    }

    /// <inheritdoc/>
    public async Task<Result<bool>> CreateApprovalStepsAsync(
        Guid leaveRequestId, User employee, CancellationToken ct = default)
    {
        // Locate the first active HR Admin — required for all routing paths.
        var hrAdmin = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Role == UserRole.HRAdmin && u.IsActive, ct);

        if (hrAdmin is null)
            return Result<bool>.Failure(
                "No active HR Admin found. Cannot route approval.", 500);

        var now   = DateTime.UtcNow;
        var steps = new List<ApprovalStep>();

        if (employee.ManagerId is not null)
        {
            // Two-step: L1 = direct manager, L2 = HR Admin.
            steps.Add(new ApprovalStep
            {
                Id             = Guid.NewGuid(),
                LeaveRequestId = leaveRequestId,
                StepNumber     = 1,
                ApproverId     = employee.ManagerId.Value,
                Status         = ApprovalStepStatus.Pending,
                CreatedAt      = now,
            });
            steps.Add(new ApprovalStep
            {
                Id             = Guid.NewGuid(),
                LeaveRequestId = leaveRequestId,
                StepNumber     = 2,
                ApproverId     = hrAdmin.Id,
                Status         = ApprovalStepStatus.Pending,
                CreatedAt      = now,
            });
        }
        else
        {
            // No manager — single step: HR Admin is L1.
            // L2 is unconditionally skipped even for retroactive requests (UT-53).
            steps.Add(new ApprovalStep
            {
                Id             = Guid.NewGuid(),
                LeaveRequestId = leaveRequestId,
                StepNumber     = 1,
                ApproverId     = hrAdmin.Id,
                Status         = ApprovalStepStatus.Pending,
                CreatedAt      = now,
            });
        }

        _db.ApprovalSteps.AddRange(steps);
        await _db.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }

    /// <inheritdoc/>
    public async Task<Result<bool>> ApproveAsync(Guid requestId, Guid approverId)
    {
        var request = await _db.LeaveRequests
            .Include(r => r.ApprovalSteps)
            .FirstOrDefaultAsync(r => r.Id == requestId);

        if (request is null)
            return Result<bool>.Failure("Leave request not found.", 404);

        if (request.Status != LeaveRequestStatus.Pending)
            return Result<bool>.Failure("Request is not in a Pending state.", 422);

        // Current active step = lowest-numbered Pending step for this request.
        var activeStep = request.ApprovalSteps
            .Where(s => s.Status == ApprovalStepStatus.Pending)
            .OrderBy(s => s.StepNumber)
            .FirstOrDefault();

        if (activeStep is null)
            return Result<bool>.Failure("No pending approval step found.", 422);

        if (activeStep.ApproverId != approverId)
            return Result<bool>.Failure("It is not your turn to approve this request.", 403);

        var now = DateTime.UtcNow;
        activeStep.Status  = ApprovalStepStatus.Approved;
        activeStep.ActedAt = now;

        // Check whether there are remaining pending steps after this one.
        var nextPendingStep = request.ApprovalSteps
            .Where(s => s.Status == ApprovalStepStatus.Pending
                     && s.StepNumber > activeStep.StepNumber)
            .OrderBy(s => s.StepNumber)
            .FirstOrDefault();

        if (nextPendingStep is not null)
        {
            // More steps remain — notify the next approver in-app.
            _db.Notifications.Add(new Notification
            {
                Id           = Guid.NewGuid(),
                UserId       = nextPendingStep.ApproverId,
                Type         = NotificationType.LeaveSubmitted,
                Title        = "Leave request awaiting your approval",
                Body         = $"A leave request (ID: {requestId}) requires your approval at step {nextPendingStep.StepNumber}.",
                ResourceType = "LeaveRequest",
                ResourceId   = requestId,
                CreatedAt    = now,
            });
        }
        else
        {
            // All steps approved — finalise the request.
            request.Status    = LeaveRequestStatus.Approved;
            request.UpdatedAt = now;

            // Notify the employee in-app.
            _db.Notifications.Add(new Notification
            {
                Id           = Guid.NewGuid(),
                UserId       = request.EmployeeId,
                Type         = NotificationType.LeaveApproved,
                Title        = "Your leave request has been approved",
                Body         = $"Your leave from {request.StartDate:yyyy-MM-dd} to {request.EndDate:yyyy-MM-dd} has been approved.",
                ResourceType = "LeaveRequest",
                ResourceId   = requestId,
                CreatedAt    = now,
            });

            // Load employee once for email + calendar (only when the services are wired).
            User? emp = null;
            if (_email is not null || _calendar is not null)
                emp = await _db.Users.FindAsync(request.EmployeeId);

            // Send approval email — plain text + inline HTML, no SendGrid template IDs (UT-54).
            if (_email is not null && emp is not null)
            {
                await _email.SendEmailAsync(
                    toEmail:  emp.Email,
                    subject:  "Your leave request has been approved",
                    textBody: $"Your leave from {request.StartDate:yyyy-MM-dd} to "
                            + $"{request.EndDate:yyyy-MM-dd} has been approved.",
                    htmlBody: $"<p>Your leave from <strong>{request.StartDate:yyyy-MM-dd}</strong> to "
                            + $"<strong>{request.EndDate:yyyy-MM-dd}</strong> has been approved.</p>");
            }

            // Create company-wide calendar event via service account (no per-user OAuth2).
            if (_calendar is not null)
            {
                await _calendar.CreateLeaveEventAsync(
                    employeeName: emp?.Email ?? request.EmployeeId.ToString(),
                    start:        request.StartDate,
                    end:          request.EndDate);
            }
        }

        await _db.SaveChangesAsync();

        await _audit.LogAsync(
            action:     "LeaveRequest.Approve",
            entityType: "LeaveRequest",
            entityId:   requestId,
            actorId:    approverId,
            oldValue:   new { Status = "Pending" },
            newValue:   new { Status = request.Status.ToString() });

        return Result<bool>.Success(true);
    }

    /// <inheritdoc/>
    public async Task<Result<bool>> RejectAsync(Guid requestId, Guid approverId, string comment)
    {
        var request = await _db.LeaveRequests
            .Include(r => r.ApprovalSteps)
            .FirstOrDefaultAsync(r => r.Id == requestId);

        if (request is null)
            return Result<bool>.Failure("Leave request not found.", 404);

        if (request.Status != LeaveRequestStatus.Pending)
            return Result<bool>.Failure("Request is not in a Pending state.", 422);

        // Current active step = lowest-numbered Pending step.
        var activeStep = request.ApprovalSteps
            .Where(s => s.Status == ApprovalStepStatus.Pending)
            .OrderBy(s => s.StepNumber)
            .FirstOrDefault();

        if (activeStep is null)
            return Result<bool>.Failure("No pending approval step found.", 422);

        if (activeStep.ApproverId != approverId)
            return Result<bool>.Failure("It is not your turn to action this request.", 403);

        var now = DateTime.UtcNow;
        activeStep.Status  = ApprovalStepStatus.Rejected;
        activeStep.ActedAt = now;
        activeStep.Comment = comment;

        request.Status    = LeaveRequestStatus.Rejected;
        request.UpdatedAt = now;

        // Restore the deducted leave balance (UT-50, UT-51).
        await _balance.RestoreBalance(request.EmployeeId, request.LeaveTypeId, request.ComputedDays);

        // Notify the employee in-app.
        _db.Notifications.Add(new Notification
        {
            Id           = Guid.NewGuid(),
            UserId       = request.EmployeeId,
            Type         = NotificationType.LeaveRejected,
            Title        = "Your leave request has been rejected",
            Body         = $"Your leave from {request.StartDate:yyyy-MM-dd} to {request.EndDate:yyyy-MM-dd} was rejected. Reason: {comment}",
            ResourceType = "LeaveRequest",
            ResourceId   = requestId,
            CreatedAt    = now,
        });

        // TODO: Enqueue rejection email via Hangfire once rejection email is confirmed.

        await _db.SaveChangesAsync();

        await _audit.LogAsync(
            action:     "LeaveRequest.Reject",
            entityType: "LeaveRequest",
            entityId:   requestId,
            actorId:    approverId,
            oldValue:   new { Status = "Pending" },
            newValue:   new { Status = "Rejected", Comment = comment });

        return Result<bool>.Success(true);
    }

    /// <inheritdoc/>
    public async Task<Result<IEnumerable<LeaveRequestDto>>> GetPendingForApproverAsync(
        Guid approverId, int page, int limit)
    {
        // Find all request IDs where approverId holds the current active (lowest) pending step
        // and no earlier step is still pending (i.e. it is genuinely their turn).
        var activeRequestIds = await _db.ApprovalSteps
            .Where(s => s.ApproverId == approverId
                     && s.Status == ApprovalStepStatus.Pending)
            .Where(s => !_db.ApprovalSteps.Any(prev =>
                prev.LeaveRequestId == s.LeaveRequestId
                && prev.StepNumber  <  s.StepNumber
                && prev.Status      == ApprovalStepStatus.Pending))
            .Select(s => s.LeaveRequestId)
            .ToListAsync();

        var requests = await _db.LeaveRequests
            .Include(r => r.ApprovalSteps.OrderBy(s => s.StepNumber))
            .Include(r => r.LeaveType)
            .Where(r => activeRequestIds.Contains(r.Id)
                     && r.Status == LeaveRequestStatus.Pending)
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync();

        return Result<IEnumerable<LeaveRequestDto>>.Success(requests.Select(MapToDto));
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────

    private static LeaveRequestDto MapToDto(LeaveRequest r) => new()
    {
        Id            = r.Id,
        EmployeeId    = r.EmployeeId,
        LeaveTypeId   = r.LeaveTypeId,
        LeaveTypeName = r.LeaveType?.Name ?? string.Empty,
        StartDate     = r.StartDate,
        EndDate       = r.EndDate,
        ComputedDays  = r.ComputedDays,
        Status        = r.Status.ToString(),
        IsRetroactive = r.IsRetroactive,
        Reason        = r.Reason,
        DocumentUrl   = r.DocumentUrl,
        CreatedAt     = r.CreatedAt,
        UpdatedAt     = r.UpdatedAt,
        ApprovalSteps = r.ApprovalSteps
            .OrderBy(s => s.StepNumber)
            .Select(s => new ApprovalStepDto
            {
                Id         = s.Id,
                StepNumber = s.StepNumber,
                ApproverId = s.ApproverId,
                Status     = s.Status.ToString(),
                ActedAt    = s.ActedAt,
                Comment    = s.Comment,
            })
            .ToList(),
    };
}
