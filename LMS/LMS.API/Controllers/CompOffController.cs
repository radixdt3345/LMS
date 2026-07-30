using System.Security.Claims;
using LMS.Application.DTOs.CompOff;
using LMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LMS.API.Controllers;

/// <summary>
/// Comp-off request and credit endpoints (COMPOFF-API-001).
///
/// Routes:
///   POST   /api/v1/comp-off/requests              — submit a request (any authenticated user)
///   POST   /api/v1/comp-off/requests/{id}/approve — Manager or HRAdmin
///   POST   /api/v1/comp-off/requests/{id}/reject  — Manager or HRAdmin
///   GET    /api/v1/comp-off/requests/me           — own requests
///   GET    /api/v1/comp-off/credits/me            — own credits
/// </summary>
[ApiController]
[Route("api/v1/comp-off")]
[Authorize]
public class CompOffController : ControllerBase
{
    private readonly ICompOffRequestService _requestService;
    private readonly ICompOffCreditService  _creditService;

    public CompOffController(
        ICompOffRequestService requestService,
        ICompOffCreditService  creditService)
    {
        _requestService = requestService;
        _creditService  = creditService;
    }

    /// <summary>
    /// POST /api/v1/comp-off/requests
    /// Submit a comp-off request for the calling user.
    /// Returns 201 on success, 409 on duplicate date, 422 on invalid hours or working day.
    /// </summary>
    [HttpPost("requests")]
    public async Task<IActionResult> Submit([FromBody] CreateCompOffRequestDto dto)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized(new { success = false, error = new { message = "Invalid token." } });

        var result = await _requestService.SubmitAsync(userId.Value, dto);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode,
                new { success = false, error = new { message = result.Error } });

        return StatusCode(201, new { success = true, data = result.Value });
    }

    /// <summary>
    /// POST /api/v1/comp-off/requests/{id}/approve
    /// Approve a pending comp-off request. Generates credit. Requires Manager or HRAdmin.
    /// </summary>
    [HttpPost("requests/{id:guid}/approve")]
    [Authorize(Policy = "ManagerOrAbove")]
    public async Task<IActionResult> Approve(Guid id)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized(new { success = false, error = new { message = "Invalid token." } });

        var result = await _requestService.ApproveAsync(id, userId.Value);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode,
                new { success = false, error = new { message = result.Error } });

        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>
    /// POST /api/v1/comp-off/requests/{id}/reject
    /// Reject a pending comp-off request. Requires Manager or HRAdmin.
    /// </summary>
    [HttpPost("requests/{id:guid}/reject")]
    [Authorize(Policy = "ManagerOrAbove")]
    public async Task<IActionResult> Reject(Guid id)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized(new { success = false, error = new { message = "Invalid token." } });

        var result = await _requestService.RejectAsync(id, userId.Value);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode,
                new { success = false, error = new { message = result.Error } });

        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>
    /// GET /api/v1/comp-off/requests/me
    /// Returns all comp-off requests for the calling user, newest first.
    /// </summary>
    [HttpGet("requests/me")]
    public async Task<IActionResult> GetMyRequests()
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized(new { success = false, error = new { message = "Invalid token." } });

        var result = await _requestService.GetMyRequestsAsync(userId.Value);
        return Ok(new
        {
            success = true,
            data    = result.Value,
            total   = result.Value!.Count,
        });
    }

    /// <summary>
    /// GET /api/v1/comp-off/credits/me
    /// Returns all comp-off credits for the calling user, ordered by expiry descending.
    /// </summary>
    [HttpGet("credits/me")]
    public async Task<IActionResult> GetMyCredits()
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized(new { success = false, error = new { message = "Invalid token." } });

        var result = await _creditService.GetMyCreditsAsync(userId.Value);
        return Ok(new
        {
            success = true,
            data    = result.Value,
            total   = result.Value!.Count,
        });
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private Guid? GetUserId()
    {
        var str = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(str, out var id) ? id : null;
    }
}
