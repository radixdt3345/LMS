using LMS.Application.DTOs.People;
using LMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LMS.API.Controllers;

/// <summary>
/// Department management endpoints.
/// GET: all authenticated users.
/// POST/PUT/DELETE: HRAdmin and SuperAdmin only.
/// FR-21 to FR-26.
/// </summary>
[ApiController]
[Route("api/v1/departments")]
[Authorize]
public class DepartmentsController : ControllerBase
{
    private readonly IDepartmentService _departmentService;

    public DepartmentsController(IDepartmentService departmentService)
    {
        _departmentService = departmentService;
    }

    /// <summary>
    /// GET /api/v1/departments — returns active departments (1h cached). FR-22.
    /// Pass ?includeInactive=true to bypass cache and return all.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetDepartments(
        [FromQuery] bool includeInactive = false, CancellationToken ct = default)
    {
        var result = await _departmentService.GetDepartmentsAsync(includeInactive, ct);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode,
                new { success = false, error = new { message = result.Error } });

        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>
    /// GET /api/v1/departments/{id} — returns a single department. FR-22.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDepartmentById(Guid id, CancellationToken ct)
    {
        var result = await _departmentService.GetDepartmentByIdAsync(id, ct);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode,
                new { success = false, error = new { message = result.Error } });

        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>
    /// POST /api/v1/departments — creates a department. HRAdmin/SuperAdmin only. FR-21.
    /// Returns 409 if name already exists.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "HRAdmin,SuperAdmin")]
    public async Task<IActionResult> CreateDepartment(
        [FromBody] CreateDepartmentRequest request, CancellationToken ct)
    {
        var result = await _departmentService.CreateDepartmentAsync(request, ct);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode,
                new { success = false, error = new { message = result.Error } });

        return StatusCode(201, new { success = true, data = result.Value });
    }

    /// <summary>
    /// PUT /api/v1/departments/{id} — updates a department. HRAdmin/SuperAdmin only. FR-23.
    /// Returns 409 if new name conflicts with an existing department.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "HRAdmin,SuperAdmin")]
    public async Task<IActionResult> UpdateDepartment(
        Guid id, [FromBody] UpdateDepartmentRequest request, CancellationToken ct)
    {
        var result = await _departmentService.UpdateDepartmentAsync(id, request, ct);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode,
                new { success = false, error = new { message = result.Error } });

        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>
    /// DELETE /api/v1/departments/{id} — soft-deletes. HRAdmin/SuperAdmin only. FR-24, FR-26.
    /// Returns 409 if department has active employees.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "HRAdmin,SuperAdmin")]
    public async Task<IActionResult> DeleteDepartment(Guid id, CancellationToken ct)
    {
        var result = await _departmentService.DeleteDepartmentAsync(id, ct);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode,
                new { success = false, error = new { message = result.Error } });

        return Ok(new { success = true });
    }
}
