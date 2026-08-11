using LMS.Application.DTOs.Reporting;
using LMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LMS.API.Controllers;

/// <summary>
/// Audit log query endpoint.
/// Returns a paginated, filterable view of the immutable audit trail.
/// Access is restricted to HRAdmin and SuperAdmin roles.
/// </summary>
[ApiController]
[Route("api/v1/audit-logs")]
[Authorize(Roles = "HRAdmin,SuperAdmin")]
public class AuditController : ControllerBase
{
    private readonly IAuditService _audit;

    public AuditController(IAuditService audit) => _audit = audit;

    /// <summary>
    /// GET /api/v1/audit-logs?entity_type=&amp;entity_id=&amp;actor_id=&amp;from=&amp;to=&amp;page=1&amp;limit=20
    /// Returns a paginated audit log, ordered newest-first.
    /// All filter parameters are optional. Maximum 100 records per page.
    /// HRAdmin and SuperAdmin only.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] AuditLogQueryDto query, CancellationToken ct)
    {
        // Enforce ceiling — clients cannot request more than 100 rows
        query.Limit = Math.Min(Math.Max(1, query.Limit), 100);
        query.Page = Math.Max(1, query.Page);

        var result = await _audit.GetAuditLogsAsync(query, ct);

        if (!result.IsSuccess)
            return StatusCode(result.StatusCode, new
            {
                success = false,
                error = new { message = result.Error }
            });

        var paged = result.Value!;
        return Ok(new
        {
            success = true,
            data = paged.Items,
            total = paged.Total,
            page = paged.Page,
            limit = paged.Limit,
        });
    }
}
