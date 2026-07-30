using LMS.Application.DTOs.LeaveRequest;
using LMS.Domain.Common;

namespace LMS.Application.Interfaces;

/// <summary>
/// Domain service contract for leave request lifecycle management.
/// Covers FR-38 to FR-46 (leave CRUD, sandwich rule, approval routing).
/// All methods return Result&lt;T&gt; — never throw for expected failures.
/// </summary>
public interface ILeaveRequestService
{
    /// <summary>
    /// Creates a leave request in Draft status for the given employee.
    /// Returns 404 if the leave type is not found or inactive.
    /// </summary>
    Task<Result<LeaveRequestDto>> CreateRequestAsync(
        Guid employeeId, CreateLeaveRequestDto dto, CancellationToken ct = default);

    /// <summary>
    /// Transitions a Draft request to Pending, computing leave days via SandwichRuleEngine.
    /// Validates overlap, document requirement, and balance sufficiency.
    /// Creates approval steps via IApprovalService.
    /// Returns 403 if caller is not the request owner.
    /// </summary>
    Task<Result<LeaveRequestDto>> SubmitRequestAsync(
        Guid requestId, Guid callerId, CancellationToken ct = default);

    /// <summary>
    /// Cancels a Draft or Pending request. Restores balance if request was Pending.
    /// Returns 403 if caller is not the request owner.
    /// </summary>
    Task<Result<LeaveRequestDto>> CancelRequestAsync(
        Guid requestId, Guid callerId, CancellationToken ct = default);

    /// <summary>
    /// Revokes a Pending or Approved request (HRAdmin+ only — enforced at controller level).
    /// Restores balance and audits the change.
    /// </summary>
    Task<Result<LeaveRequestDto>> RevokeRequestAsync(
        Guid requestId, Guid callerId, CancellationToken ct = default);

    /// <summary>
    /// Returns the calling employee's own leave requests, newest first, paginated.
    /// Page is 1-based; limit is clamped to 1–100.
    /// </summary>
    Task<Result<PagedResult<LeaveRequestDto>>> GetMyRequestsAsync(
        Guid employeeId, int page, int limit, CancellationToken ct = default);
}
