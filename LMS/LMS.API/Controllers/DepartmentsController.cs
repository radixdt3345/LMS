using LMS.Application.DTOs.People;
using LMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LMS.API.Controllers;

/// <summary>
/// Department CRUD endpoints.
/// GET (list + single) — any authenticated user.
/// POST / PUT / DELETE — HRAdmin or SuperAdmin only.
/// </summary>
[ApiController]
[Route("api/v1/departments")]
[Authorize]
public class DepartmentsController : ControllerBase
{
    private readonly IDepartmentService _departments;

    public DepartmentsController(IDepartmentService departments)
    {
        _departments = departments;
    }

    /// <summary>GET /api/v1/departments?page=1&amp;limit=20</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        var result = await _departments.GetAllAsync(page, limit, ct);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode,
                new { success = false, error = new { message = result.Error } });

        var r = result.Value!;
        return Ok(new
        {
            success = true,
            data = new
            {
                items = r.Items,
                total = r.Total,
                page = r.Page,
                limit = r.Limit,
            },
        });
    }

    /// <summary>GET /api/v1/departments/{id}</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id, CancellationToken ct = default)
    {
        var result = await _departments.GetByIdAsync(id, ct);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode,
                new { success = false, error = new { message = result.Error } });

        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>POST /api/v1/departments — HRAdmin / SuperAdmin only.</summary>
    [HttpPost]
    [Authorize(Roles = "HRAdmin,SuperAdmin")]
    public async Task<IActionResult> Create(
        [FromBody] CreateDepartmentDto dto,
        CancellationToken ct = default)
    {
        var result = await _departments.CreateAsync(dto, ct);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode,
                new { success = false, error = new { message = result.Error } });

        return StatusCode(201, new { success = true, data = result.Value });
    }

    /// <summary>PUT /api/v1/departments/{id} — HRAdmin / SuperAdmin only.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "HRAdmin,SuperAdmin")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateDepartmentDto dto,
        CancellationToken ct = default)
    {
        var result = await _departments.UpdateAsync(id, dto, ct);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode,
                new { success = false, error = new { message = result.Error } });

        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>DELETE /api/v1/departments/{id} — HRAdmin / SuperAdmin only. Soft-deletes.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "HRAdmin,SuperAdmin")]
    public async Task<IActionResult> Delete(
        Guid id, CancellationToken ct = default)
    {
        var result = await _departments.DeleteAsync(id, ct);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode,
                new { success = false, error = new { message = result.Error } });

        return NoContent();
    }
}
