using System.Security.Claims;
using LMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LMS.API.Controllers;

/// <summary>
/// Approval engine endpoints (LEAVECORE-API-005).
///
/// Routes:
///   GET  /api/v1/approvals/pending      — paginated list of requests awaiting the caller's action
///   POST /api/v1/approvals/{id}/approve — approve the current pending step
///   POST /api/v1/approvals/{id}/reject  — reject the current pending step
///
/// All endpoints require the ManagerOrAbove authorization policy
/// (role claim: Manager | HRAdmin | SuperAdmin).
/// </summary>
[ApiController]
[Route("api/v1/approvals")]
[Authorize(Policy = "ManagerOrAbove")]
public class ApprovalsController : ControllerBase
{
    private readonly IApprovalService _approvalService;

    public ApprovalsController(IApprovalService approvalService)
    {
        _approvalService = approvalService;
    }

    /// <summary>
    /// GET /api/v1/approvals/pending
    /// Returns leave requests where the calling approver holds the current active pending step.
    /// Supports ?page=1&amp;limit=20 pagination (limit clamped 1–100).
    /// </summary>
    [HttpGet("pending")]
    public async Task<IActionResult> GetPending(
        [FromQuery] int page  = 1,
        [FromQuery] int limit = 20)
    {
        var approverId = GetUserId();
        if (approverId is null)
            return Unauthorized(new { success = false, error = new { message = "Invalid token." } });

        page  = Math.Max(page, 1);
        limit = Math.Clamp(limit, 1, 100);

        var result = await _approvalService.GetPendingForApproverAsync(approverId.Value, page, limit);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode,
                new { success = false, error = new { message = result.Error } });

        var items = result.Value!.ToList();
        return Ok(new
        {
            success = true,
            data    = items,
            total   = items.Count,
            page,
            limit,
        });
    }

    /// <summary>
    /// POST /api/v1/approvals/{id}/approve
    /// Approves the current pending step on leave request {id} on behalf of the caller.
    /// Returns 200 on success; 404/403/422 on failure.
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id)
    {
        var approverId = GetUserId();
        if (approverId is null)
            return Unauthorized(new { success = false, error = new { message = "Invalid token." } });

        var result = await _approvalService.ApproveAsync(id, approverId.Value);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode,
                new { success = false, error = new { message = result.Error } });

        return Ok(new { success = true, data = new { message = "Leave request approved." } });
    }

    /// <summary>
    /// POST /api/v1/approvals/{id}/reject
    /// Rejects the current pending step on leave request {id} on behalf of the caller.
    /// Body: { "comment": "reason" }
    /// Returns 200 on success; 404/403/422 on failure.
    /// </summary>
    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectRequestDto dto)
    {
        var approverId = GetUserId();
        if (approverId is null)
            return Unauthorized(new { success = false, error = new { message = "Invalid token." } });

        var result = await _approvalService.RejectAsync(
            id, approverId.Value, dto.Comment ?? string.Empty);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode,
                new { success = false, error = new { message = result.Error } });

        return Ok(new { success = true, data = new { message = "Leave request rejected." } });
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private Guid? GetUserId()
    {
        var str = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(str, out var id) ? id : null;
    }
}

/// <summary>Request body for the reject endpoint.</summary>
public sealed record RejectRequestDto(string? Comment);
