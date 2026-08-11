using LMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace LMS.API.Controllers;

[ApiController]
[Route("api/v1/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IReportService _reports;

    public DashboardController(IReportService reports) => _reports = reports;

    private Guid CurrentUserId =>
        Guid.TryParse(
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value,
            out var id) ? id : Guid.Empty;

    /// <summary>GET /api/v1/dashboard/employee — any authenticated user</summary>
    [HttpGet("employee")]
    public async Task<IActionResult> GetEmployeeDashboardAsync()
    {
        var result = await _reports.GetEmployeeDashboardAsync(CurrentUserId);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode,
                new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>GET /api/v1/dashboard/manager — Manager, HRAdmin, SuperAdmin</summary>
    [HttpGet("manager")]
    [Authorize(Roles = "Manager,HRAdmin,SuperAdmin")]
    public async Task<IActionResult> GetManagerDashboardAsync()
    {
        var result = await _reports.GetManagerDashboardAsync(CurrentUserId);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode,
                new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>GET /api/v1/dashboard/hr — HRAdmin, SuperAdmin</summary>
    [HttpGet("hr")]
    [Authorize(Roles = "HRAdmin,SuperAdmin")]
    public async Task<IActionResult> GetHrDashboardAsync()
    {
        var result = await _reports.GetHrDashboardAsync();
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode,
                new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>GET /api/v1/dashboard/super-admin — SuperAdmin only</summary>
    [HttpGet("super-admin")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> GetSuperAdminDashboardAsync()
    {
        var result = await _reports.GetSuperAdminDashboardAsync();
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode,
                new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = result.Value });
    }
}
