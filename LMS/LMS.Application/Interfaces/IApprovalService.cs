using LMS.Domain.Common;
using LMS.Domain.Entities;

namespace LMS.Application.Interfaces;

/// <summary>
/// Approval routing service — creates the ordered approval step chain for a leave request.
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
}
