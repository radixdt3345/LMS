using LMS.Application.DTOs.LeaveCore;
using LMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LMS.API.Controllers;

/// <summary>
/// Holiday calendar endpoints.
/// Read endpoints are open to all authenticated users.
/// Write endpoints (POST/PUT/DELETE) require SuperAdmin or HrAdmin role.
/// </summary>
[ApiController]
[Route("api/v1/holidays")]
[Authorize]
public class HolidayController : ControllerBase
{
    private readonly IHolidayService _holidays;

    public HolidayController(IHolidayService holidays) => _holidays = holidays;

    /// <summary>
    /// GET /api/v1/holidays?year=2026
    /// Returns all holidays for the given year (including recurring ones).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetHolidays(
        [FromQuery] int year, CancellationToken ct)
    {
        if (year is < 2000 or > 2100)
            return BadRequest(new
            {
                success = false,
                error = new { code = "INVALID_YEAR", message = "Year must be between 2000 and 2100." }
            });

        var result = await _holidays.GetHolidaysAsync(year, ct);
        return result.IsSuccess
            ? Ok(new { success = true, data = result.Value })
            : StatusCode(result.StatusCode,
                new { success = false, error = new { code = "HOLIDAY_ERROR", message = result.Error } });
    }

    /// <summary>
    /// POST /api/v1/holidays
    /// Creates a new holiday. SuperAdmin or HrAdmin only.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "SuperAdmin,HrAdmin")]
    public async Task<IActionResult> CreateHoliday(
        [FromBody] CreateHolidayDto dto, CancellationToken ct)
    {
        var result = await _holidays.CreateHolidayAsync(dto, ct);
        return result.IsSuccess
            ? CreatedAtAction(
                nameof(GetHolidays),
                new { year = dto.Date.Year },
                new { success = true, data = result.Value })
            : StatusCode(result.StatusCode,
                new { success = false, error = new { code = "CREATE_HOLIDAY_ERROR", message = result.Error } });
    }

    /// <summary>
    /// PUT /api/v1/holidays/{id}
    /// Partial update of a holiday. SuperAdmin or HrAdmin only.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,HrAdmin")]
    public async Task<IActionResult> UpdateHoliday(
        Guid id, [FromBody] UpdateHolidayDto dto, CancellationToken ct)
    {
        var result = await _holidays.UpdateHolidayAsync(id, dto, ct);
        return result.IsSuccess
            ? Ok(new { success = true, data = result.Value })
            : StatusCode(result.StatusCode,
                new { success = false, error = new { code = "UPDATE_HOLIDAY_ERROR", message = result.Error } });
    }

    /// <summary>
    /// DELETE /api/v1/holidays/{id}
    /// Hard-deletes a holiday. SuperAdmin or HrAdmin only.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,HrAdmin")]
    public async Task<IActionResult> DeleteHoliday(Guid id, CancellationToken ct)
    {
        var result = await _holidays.DeleteHolidayAsync(id, ct);
        return result.IsSuccess
            ? Ok(new { success = true, data = new { deleted = true } })
            : StatusCode(result.StatusCode,
                new { success = false, error = new { code = "DELETE_HOLIDAY_ERROR", message = result.Error } });
    }

    /// <summary>
    /// GET /api/v1/holidays/working-days?start=2026-07-27&amp;end=2026-07-31
    /// Counts Mon-Fri days in [start, end] inclusive, excluding registered holidays.
    /// Used by the frontend leave-request form to display net working days.
    /// </summary>
    [HttpGet("working-days")]
    public async Task<IActionResult> GetWorkingDays(
        [FromQuery] DateOnly start, [FromQuery] DateOnly end, CancellationToken ct)
    {
        var result = await _holidays.CountWorkingDaysAsync(start, end, ct);
        return result.IsSuccess
            ? Ok(new { success = true, data = new { start, end, workingDays = result.Value } })
            : StatusCode(result.StatusCode,
                new { success = false, error = new { code = "WORKING_DAYS_ERROR", message = result.Error } });
    }
}
