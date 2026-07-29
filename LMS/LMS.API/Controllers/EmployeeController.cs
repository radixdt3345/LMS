using System.Security.Claims;
using LMS.Application.DTOs.People;
using LMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LMS.API.Controllers;

/// <summary>
/// Employee management endpoints. FR-12 to FR-20.
/// /me routes must be declared before {id:guid} to avoid ambiguity.
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

    // ─── Self-service /me endpoints (declared before {id:guid}) ─────────────────

    /// <summary>
    /// GET /api/v1/employees/me
    /// Returns the authenticated user's own profile. Available to any authenticated user.
    /// </summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetMyProfile(CancellationToken ct)
    {
        var userId = GetCallerId();
        if (userId == Guid.Empty)
            return Unauthorized(new { success = false, error = new { message = "Invalid token." } });

        var result = await _employees.GetMyProfileAsync(userId, ct);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode,
                new { success = false, error = new { message = result.Error } });

        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>
    /// PUT /api/v1/employees/me
    /// Self-service profile update. Only firstName, lastName, and phone are mutable. FR-14.
    /// </summary>
    [HttpPut("me")]
    public async Task<IActionResult> UpdateMyProfile(
        [FromBody] UpdateMyProfileDto dto, CancellationToken ct)
    {
        var userId = GetCallerId();
        if (userId == Guid.Empty)
            return Unauthorized(new { success = false, error = new { message = "Invalid token." } });

        var result = await _employees.UpdateMyProfileAsync(userId, dto, ct);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode,
                new { success = false, error = new { message = result.Error } });

        return Ok(new { success = true, data = result.Value });
    }

    // ─── Standard CRUD ───────────────────────────────────────────────────────────

    /// <summary>
    /// GET /api/v1/employees?page=1&amp;limit=20&amp;departmentId=&amp;search=
    /// Paginated active employee list. HRAdmin and SuperAdmin only. FR-12.
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
    /// Returns a single employee by ID. Returns 404 when not found. FR-12.
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
    /// GET /api/v1/employees/{id}/team
    /// Returns active direct reports of the specified manager.
    /// Caller must be the manager themselves or HRAdmin/SuperAdmin. FR-20.
    /// </summary>
    [HttpGet("{id:guid}/team")]
    [Authorize(Roles = "Manager,HRAdmin,SuperAdmin")]
    public async Task<IActionResult> GetTeam(Guid id, CancellationToken ct)
    {
        var callerId = GetCallerId();
        if (callerId == Guid.Empty)
            return Unauthorized(new { success = false, error = new { message = "Invalid token." } });

        var callerIsHrAdmin = User.IsInRole("HRAdmin") || User.IsInRole("SuperAdmin");
        var result = await _employees.GetTeamAsync(id, callerId, callerIsHrAdmin, ct);

        if (!result.IsSuccess)
            return StatusCode(result.StatusCode,
                new { success = false, error = new { message = result.Error } });

        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>
    /// POST /api/v1/employees
    /// Creates a new employee. Returns 409 if email already registered. FR-13.
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
    /// Soft-deactivates an employee (is_active = false). Idempotent. FR-15.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "HRAdmin,SuperAdmin")]
    public async Task<IActionResult> DeactivateEmployee(Guid id, CancellationToken ct)
    {
        var result = await _employees.DeactivateEmployeeAsync(id, ct);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode,
                new { success = false, error = new { message = result.Error } });

        return NoContent();
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────────

    /// <summary>Extracts the caller's user ID from the JWT sub claim.</summary>
    private Guid GetCallerId()
    {
        var raw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(raw, out var id) ? id : Guid.Empty;
    }
}
