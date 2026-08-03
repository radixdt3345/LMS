using LMS.Application.DTOs.LeaveRequest;
using LMS.Domain.Common;
using LMS.Domain.Entities;

namespace LMS.Application.Interfaces;

/// <summary>
/// Approval routing service — creates and drives the ordered approval step chain for leave requests.
///
/// Routing rules (UT-53, IT-40):
/// - employee.ManagerId IS NOT NULL: Step 1 = direct manager, Step 2 = HR Admin.
/// - employee.ManagerId IS NULL:     Step 1 = HR Admin ONLY.
///   L2 is unconditionally skipped — even for retroactive requests.
/// </summary>
public interface IApprovalService
{
    /// <summary>
    /// Creates and persists the approval steps for <paramref name="leaveRequestId"/>.
    /// Routing is determined solely by <paramref name="employee"/>.ManagerId.
    /// Returns 500 if no active HR Admin user exists in the system.
    /// </summary>
    Task<Result<bool>> CreateApprovalStepsAsync(
        Guid leaveRequestId, User employee, CancellationToken ct = default);

    /// <summary>
    /// Records the approver's approval action on the current pending step.
    /// When all steps are approved, finalises the request (Status = Approved),
    /// creates an in-app notification for the employee, and enqueues email
    /// and Google Calendar events via Hangfire.
    /// When more steps remain, notifies the next approver in-app.
    ///
    /// Returns 404 if request not found; 422 if request is not Pending;
    /// 403 if <paramref name="approverId"/> is not the current step's approver.
    /// </summary>
    Task<Result<bool>> ApproveAsync(Guid requestId, Guid approverId);

    /// <summary>
    /// Rejects the leave request at the current pending approval step.
    /// Sets Status = Rejected, restores the deducted balance,
    /// creates an in-app rejection notification for the employee,
    /// enqueues a rejection email via Hangfire, and audits the action.
    ///
    /// Returns 404 if request not found; 422 if not Pending;
    /// 403 if <paramref name="approverId"/> is not the current step's approver.
    /// </summary>
    Task<Result<bool>> RejectAsync(Guid requestId, Guid approverId, string comment);

    /// <summary>
    /// Returns a paginated list of leave requests where <paramref name="approverId"/> is
    /// the designated approver for the current (lowest-numbered) pending step.
    /// Results are ordered newest-first by request creation date.
    /// </summary>
    Task<Result<IEnumerable<LeaveRequestDto>>> GetPendingForApproverAsync(
        Guid approverId, int page, int limit);
}
