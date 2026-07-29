using System.Security.Claims;
using LMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LMS.API.Controllers;

/// <summary>
/// Leave balance endpoints — query current and historical leave balances.
/// </summary>
[ApiController]
[Route("api/v1/leave-balances")]
[Authorize]
public class LeaveBalanceController : ControllerBase
{
    private readonly ILeaveBalanceService _balanceService;

    public LeaveBalanceController(ILeaveBalanceService balanceService)
    {
        _balanceService = balanceService;
    }

    /// <summary>
    /// GET /api/v1/leave-balances/me?year=YYYY
    /// Returns the calling user's leave balances for the specified year
    /// (defaults to the current calendar year).
    /// Any authenticated user may call this endpoint.
    /// </summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetMyBalances([FromQuery] int? year)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            return Unauthorized(new { success = false, error = new { message = "Invalid token." } });

        var targetYear = year ?? DateTime.UtcNow.Year;
        var balances   = await _balanceService.GetBalance(userId, targetYear);

        return Ok(new
        {
            success = true,
            data    = balances,
            total   = balances.Count,
        });
    }

    /// <summary>
    /// GET /api/v1/leave-balances/{userId}?year=YYYY
    /// Returns leave balances for the specified employee for the given year
    /// (defaults to the current calendar year).
    /// Requires HRAdmin or SuperAdmin role (HRAdminOrAbove policy).
    /// </summary>
    [HttpGet("{userId:guid}")]
    [Authorize(Policy = "HRAdminOrAbove")]
    public async Task<IActionResult> GetBalanceForUser(
        Guid userId, [FromQuery] int? year)
    {
        var targetYear = year ?? DateTime.UtcNow.Year;
        var balances   = await _balanceService.GetBalance(userId, targetYear);

        return Ok(new
        {
            success = true,
            data    = balances,
            total   = balances.Count,
        });
    }
}
