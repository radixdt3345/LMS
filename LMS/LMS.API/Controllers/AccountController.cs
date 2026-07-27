using LMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LMS.API.Controllers;

/// <summary>
/// Account management endpoints — list accounts with lockout status, unlock a user.
/// All endpoints require HRAdmin or SuperAdmin role. FR-10, FR-11.
/// </summary>
[ApiController]
[Route("api/v1/auth/accounts")]
[Authorize(Roles = "HRAdmin,SuperAdmin")]
public class AccountController : ControllerBase
{
    private readonly IAccountService _accountService;

    public AccountController(IAccountService accountService)
    {
        _accountService = accountService;
    }

    /// <summary>
    /// GET /api/v1/auth/accounts?page=1&amp;limit=20
    /// Returns a paginated list of all user accounts with current lockout status.
    /// HRAdmin and SuperAdmin only. FR-10.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAccounts(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        var result = await _accountService.GetAccountsAsync(page, limit, ct);

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

    /// <summary>
    /// POST /api/v1/auth/accounts/{userId}/unlock
    /// Clears FailedLoginCount and LockoutUntil for the specified user.
    /// HRAdmin and SuperAdmin only. Returns 403 for lower roles (enforced by [Authorize]).
    /// Returns 404 if userId does not exist. FR-11.
    /// </summary>
    [HttpPost("{userId:guid}/unlock")]
    public async Task<IActionResult> UnlockAccount(
        [FromRoute] Guid userId, CancellationToken ct)
    {
        var result = await _accountService.UnlockAccountAsync(userId, ct);

        if (!result.IsSuccess)
            return StatusCode(result.StatusCode, new
            {
                success = false,
                error = new { message = result.Error }
            });

        return Ok(new { success = true });
    }
}
