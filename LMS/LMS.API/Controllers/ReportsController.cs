using LMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LMS.API.Controllers;

[ApiController]
[Route("api/v1/reports")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reports;

    public ReportsController(IReportService reports) => _reports = reports;

    /// <summary>GET /api/v1/reports/utilization?year=2026&amp;departmentId=guid</summary>
    [HttpGet("utilization")]
    [Authorize(Roles = "HRAdmin,SuperAdmin")]
    public async Task<IActionResult> GetUtilizationAsync(
        [FromQuery] int year,
        [FromQuery] Guid? departmentId = null)
    {
        var result = await _reports.GetUtilizationAsync(year, departmentId);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode,
                new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>GET /api/v1/reports/trends?months=6</summary>
    [HttpGet("trends")]
    [Authorize(Roles = "HRAdmin,SuperAdmin")]
    public async Task<IActionResult> GetTrendsAsync([FromQuery] int months = 6)
    {
        var result = await _reports.GetTrendsAsync(months);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode,
                new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>GET /api/v1/reports/compliance</summary>
    [HttpGet("compliance")]
    [Authorize(Roles = "HRAdmin,SuperAdmin")]
    public async Task<IActionResult> GetComplianceAsync()
    {
        var result = await _reports.GetComplianceAsync();
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode,
                new { success = false, error = new { message = result.Error } });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>GET /api/v1/reports/export?type=utilization|trends — streaming CSV</summary>
    [HttpGet("export")]
    [Authorize(Roles = "HRAdmin,SuperAdmin")]
    public async Task ExportCsvAsync([FromQuery] string type = "utilization")
    {
        Response.ContentType = "text/csv";
        Response.Headers["Content-Disposition"] =
            $"attachment; filename=\"{type}-report.csv\"";

        using var writer = new System.IO.StreamWriter(
            Response.Body, System.Text.Encoding.UTF8,
            bufferSize: 1024, leaveOpen: true);

        await foreach (var line in _reports.ExportCsvAsync(type))
        {
            await writer.WriteLineAsync(line);
            await writer.FlushAsync();
        }
    }
}
