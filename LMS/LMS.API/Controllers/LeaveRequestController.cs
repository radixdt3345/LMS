using System.Security.Claims;
using LMS.Application.DTOs.LeaveRequest;
using LMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LMS.API.Controllers;

/// <summary>
/// Leave request endpoints — create, submit, cancel, revoke, and list own requests.
/// All endpoints require an authenticated JWT. Revoke additionally requires HRAdminOrAbove.
/// Base: /api/v1/leave-requests
/// </summary>
[ApiController]
[Route("api/v1/leave-requests")]
[Authorize]
public class LeaveRequestController : ControllerBase
{
    private readonly ILeaveRequestService _service;

    public LeaveRequestController(ILeaveRequestService service)
    {
        _service = service;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Guid? GetCallerId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    // ── POST /api/v1/leave-requests → 201 Draft ───────────────────────────────

    /// <summary>
    /// Creates a leave request in Draft status.
    /// The employee is derived from the JWT — callers can only create for themselves.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateRequest(
        [FromBody] CreateLeaveRequestDto dto,
        CancellationToken ct = default)
    {
        var callerId = GetCallerId();
        if (callerId is null)
            return Unauthorized(new
            {
                success = false,
                error   = new { message = "Invalid or missing authentication token." }
            });

        var result = await _service.CreateRequestAsync(callerId.Value, dto, ct);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode, new
            {
                success = false,
                error   = new { message = result.Error }
            });

        return StatusCode(201, new { success = true, data = result.Value });
    }

    // ── POST /api/v1/leave-requests/{id}/submit → 200 Pending ─────────────────

    /// <summary>
    /// Transitions a Draft request to Pending, triggering SandwichRuleEngine,
    /// balance deduction, and approval step creation.
    /// Only the request owner may call this endpoint.
    /// </summary>
    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> SubmitRequest(
        Guid id, CancellationToken ct = default)
    {
        var callerId = GetCallerId();
        if (callerId is null)
            return Unauthorized(new
            {
                success = false,
                error   = new { message = "Invalid or missing authentication token." }
            });

        var result = await _service.SubmitRequestAsync(id, callerId.Value, ct);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode, new
            {
                success = false,
                error   = new { message = result.Error }
            });

        return Ok(new { success = true, data = result.Value });
    }

    // ── POST /api/v1/leave-requests/{id}/cancel → 200 ─────────────────────────

    /// <summary>
    /// Cancels a Draft or Pending leave request.
    /// Only the request owner may cancel. Balance is restored if the request was Pending.
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> CancelRequest(
        Guid id, CancellationToken ct = default)
    {
        var callerId = GetCallerId();
        if (callerId is null)
            return Unauthorized(new
            {
                success = false,
                error   = new { message = "Invalid or missing authentication token." }
            });

        var result = await _service.CancelRequestAsync(id, callerId.Value, ct);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode, new
            {
                success = false,
                error   = new { message = result.Error }
            });

        return Ok(new { success = true, data = result.Value });
    }

    // ── POST /api/v1/leave-requests/{id}/revoke → 200 (HRAdmin+) ─────────────

    /// <summary>
    /// Revokes a Pending or Approved leave request.
    /// Requires HRAdmin or SuperAdmin role (HRAdminOrAbove policy).
    /// Balance is restored on revoke.
    /// </summary>
    [HttpPost("{id:guid}/revoke")]
    [Authorize(Policy = "HRAdminOrAbove")]
    public async Task<IActionResult> RevokeRequest(
        Guid id, CancellationToken ct = default)
    {
        var callerId = GetCallerId();
        if (callerId is null)
            return Unauthorized(new
            {
                success = false,
                error   = new { message = "Invalid or missing authentication token." }
            });

        var result = await _service.RevokeRequestAsync(id, callerId.Value, ct);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode, new
            {
                success = false,
                error   = new { message = result.Error }
            });

        return Ok(new { success = true, data = result.Value });
    }

    // ── GET /api/v1/leave-requests → 200 paginated (own) ─────────────────────

    /// <summary>
    /// Returns the calling user's own leave requests, newest first.
    /// Supports ?page=1&amp;limit=20 (max limit = 100).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMyRequests(
        [FromQuery] int page  = 1,
        [FromQuery] int limit = 20,
        CancellationToken ct  = default)
    {
        var callerId = GetCallerId();
        if (callerId is null)
            return Unauthorized(new
            {
                success = false,
                error   = new { message = "Invalid or missing authentication token." }
            });

        var result = await _service.GetMyRequestsAsync(callerId.Value, page, limit, ct);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode, new
            {
                success = false,
                error   = new { message = result.Error }
            });

        return Ok(new
        {
            success = true,
            data    = result.Value!.Items,
            total   = result.Value.Total,
            page    = result.Value.Page,
            limit   = result.Value.Limit,
        });
    }
}
