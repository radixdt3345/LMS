using LMS.Application.DTOs.LeaveCore;
using LMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LMS.API.Controllers;

/// <summary>
/// Leave type management endpoints.
/// Reads: all authenticated users. Writes: SuperAdmin only.
/// FR-27 to FR-30.
/// </summary>
[ApiController]
[Route("api/v1/leave-types")]
[Authorize]
public class LeaveTypeController : ControllerBase
{
    private readonly ILeaveTypeService _leaveTypeService;

    public LeaveTypeController(ILeaveTypeService leaveTypeService)
    {
        _leaveTypeService = leaveTypeService;
    }

    /// <summary>
    /// GET /api/v1/leave-types — returns active leave types (or all if includeInactive=true).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetLeaveTypes(
        [FromQuery] bool includeInactive = false, CancellationToken ct = default)
    {
        var result = await _leaveTypeService.GetLeaveTypesAsync(includeInactive, ct);

        if (!result.IsSuccess)
            return StatusCode(result.StatusCode, new
            {
                success = false,
                error = new { message = result.Error }
            });

        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>
    /// GET /api/v1/leave-types/{id} — returns a single leave type by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetLeaveTypeById(Guid id, CancellationToken ct)
    {
        var result = await _leaveTypeService.GetLeaveTypeByIdAsync(id, ct);

        if (!result.IsSuccess)
            return StatusCode(result.StatusCode, new
            {
                success = false,
                error = new { message = result.Error }
            });

        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>
    /// POST /api/v1/leave-types — creates a new leave type. SuperAdmin only. FR-27.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> CreateLeaveType(
        [FromBody] CreateLeaveTypeDto dto, CancellationToken ct)
    {
        var result = await _leaveTypeService.CreateLeaveTypeAsync(dto, ct);

        if (!result.IsSuccess)
            return StatusCode(result.StatusCode, new
            {
                success = false,
                error = new { message = result.Error }
            });

        return StatusCode(201, new { success = true, data = result.Value });
    }

    /// <summary>
    /// PUT /api/v1/leave-types/{id} — updates a leave type. SuperAdmin only. FR-28.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> UpdateLeaveType(
        Guid id, [FromBody] UpdateLeaveTypeDto dto, CancellationToken ct)
    {
        var result = await _leaveTypeService.UpdateLeaveTypeAsync(id, dto, ct);

        if (!result.IsSuccess)
            return StatusCode(result.StatusCode, new
            {
                success = false,
                error = new { message = result.Error }
            });

        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>
    /// DELETE /api/v1/leave-types/{id} — soft-deletes a leave type. SuperAdmin only. FR-29.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> DeactivateLeaveType(Guid id, CancellationToken ct)
    {
        var result = await _leaveTypeService.DeactivateLeaveTypeAsync(id, ct);

        if (!result.IsSuccess)
            return StatusCode(result.StatusCode, new
            {
                success = false,
                error = new { message = result.Error }
            });

        return Ok(new { success = true });
    }
}
