using LMS.Application.DTOs.People;
using LMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LMS.API.Controllers;

/// <summary>
/// Employee management endpoints. FR-12 to FR-20.
/// GET /api/v1/employees (list) and GET /api/v1/employees/{id} require HRAdmin or SuperAdmin.
/// POST / PUT / DELETE also require HRAdmin or SuperAdmin.
/// </summary>
[ApiController]
[Route("api/v1/employees")]
[Authorize]
public class EmployeeController : ControllerBase
{
    private readonly IEmployeeService _employees;

    public EmployeeController(IEmployeeService employees)
    {
        _employees = employees;
    }

    /// <summary>
    /// GET /api/v1/employees?page=1&amp;limit=20&amp;departmentId=&amp;search=
    /// Returns paginated active employees. Optional departmentId filter and free-text search.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "HRAdmin,SuperAdmin")]
    public async Task<IActionResult> GetEmployees(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        [FromQuery] Guid? departmentId = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var result = await _employees.GetEmployeesAsync(page, limit, departmentId, search, ct);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode,
                new { success = false, error = new { message = result.Error } });

        var v = result.Value!;
        return Ok(new { success = true, data = v.Items, total = v.Total, page = v.Page, limit = v.Limit });
    }

    /// <summary>
    /// GET /api/v1/employees/{id}
    /// Returns a single employee by ID. Returns 404 when not found.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "HRAdmin,SuperAdmin")]
    public async Task<IActionResult> GetEmployeeById(Guid id, CancellationToken ct)
    {
        var result = await _employees.GetEmployeeByIdAsync(id, ct);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode,
                new { success = false, error = new { message = result.Error } });

        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>
    /// POST /api/v1/employees
    /// Creates a new employee. Returns 409 if email is already registered. FR-13.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "HRAdmin,SuperAdmin")]
    public async Task<IActionResult> CreateEmployee(
        [FromBody] CreateEmployeeDto dto, CancellationToken ct)
    {
        var result = await _employees.CreateEmployeeAsync(dto, ct);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode,
                new { success = false, error = new { message = result.Error } });

        return StatusCode(201, new { success = true, data = result.Value });
    }

    /// <summary>
    /// PUT /api/v1/employees/{id}
    /// Patches employee profile (null fields unchanged). Returns 404 when not found. FR-14.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "HRAdmin,SuperAdmin")]
    public async Task<IActionResult> UpdateEmployee(
        Guid id, [FromBody] UpdateEmployeeDto dto, CancellationToken ct)
    {
        var result = await _employees.UpdateEmployeeAsync(id, dto, ct);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode,
                new { success = false, error = new { message = result.Error } });

        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>
    /// DELETE /api/v1/employees/{id}
    /// Soft-deletes (deactivates) an employee. Idempotent. Returns 404 when not found. FR-15.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "HRAdmin,SuperAdmin")]
    public async Task<IActionResult> DeactivateEmployee(Guid id, CancellationToken ct)
    {
        var result = await _employees.DeactivateEmployeeAsync(id, ct);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode,
                new { success = false, error = new { message = result.Error } });

        return Ok(new { success = true });
    }
}
