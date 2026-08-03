using System.Security.Claims;
using LMS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LMS.API.Controllers;

/// <summary>
/// In-app notification endpoints.
/// All routes require a valid Bearer JWT.
/// </summary>
[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notifications;

    public NotificationsController(INotificationService notifications)
    {
        _notifications = notifications;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Guid CurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id)
            ? id
            : throw new InvalidOperationException("JWT sub claim is missing or not a valid GUID.");
    }

    // ── GET /api/v1/notifications ─────────────────────────────────────────────

    /// <summary>Returns the 20 most-recent notifications for the authenticated user.</summary>
    [HttpGet]
    public async Task<IActionResult> GetRecent(CancellationToken ct)
    {
        var result = await _notifications.GetRecentAsync(CurrentUserId(), limit: 20, ct);

        if (!result.IsSuccess)
            return StatusCode(result.StatusCode, new
            {
                success = false,
                error = new { message = result.Error }
            });

        return Ok(new { success = true, data = result.Value });
    }

    // ── GET /api/v1/notifications/unread-count ────────────────────────────────

    /// <summary>Returns the count of unread notifications for the authenticated user.</summary>
    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken ct)
    {
        var result = await _notifications.GetUnreadCountAsync(CurrentUserId(), ct);

        if (!result.IsSuccess)
            return StatusCode(result.StatusCode, new
            {
                success = false,
                error = new { message = result.Error }
            });

        return Ok(new { success = true, data = new { count = result.Value } });
    }

    // ── POST /api/v1/notifications/{id}/read ─────────────────────────────────

    /// <summary>Marks a single notification as read. Returns 204 on success.</summary>
    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        var result = await _notifications.MarkReadAsync(id, CurrentUserId(), ct);

        if (!result.IsSuccess)
            return StatusCode(result.StatusCode, new
            {
                success = false,
                error = new { message = result.Error }
            });

        return NoContent();
    }

    // ── POST /api/v1/notifications/read-all ──────────────────────────────────

    /// <summary>Marks all notifications for the authenticated user as read. Returns 204.</summary>
    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        var result = await _notifications.MarkAllReadAsync(CurrentUserId(), ct);

        if (!result.IsSuccess)
            return StatusCode(result.StatusCode, new
            {
                success = false,
                error = new { message = result.Error }
            });

        return NoContent();
    }
}
