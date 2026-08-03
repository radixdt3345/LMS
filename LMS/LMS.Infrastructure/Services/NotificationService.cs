using LMS.Application.DTOs.Notification;
using LMS.Application.Interfaces;
using LMS.Domain.Common;
using LMS.Domain.Entities;
using LMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LMS.Infrastructure.Services;

/// <summary>
/// In-app notification CRUD.
/// Every mutation calls AuditService.LogAsync (constitution requirement).
/// Never throws for expected failures — returns Result&lt;T&gt;.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly LmsDbContext _db;
    private readonly IAuditService _audit;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        LmsDbContext db,
        IAuditService audit,
        ILogger<NotificationService> logger)
    {
        _db = db;
        _audit = audit;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<Result<Guid>> CreateNotificationAsync(
        Guid userId,
        string title,
        string body,
        string? resourceType = null,
        Guid? resourceId = null,
        CancellationToken ct = default)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = title,
            Body = body,
            IsRead = false,
            ResourceType = resourceType,
            ResourceId = resourceId,
            CreatedAt = DateTime.UtcNow
        };

        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(
            actorUserId: userId,
            action: "notification.created",
            entityType: "Notification",
            entityId: notification.Id,
            details: $"title={title}",
            ct: ct);

        _logger.LogInformation(
            "Notification {NotificationId} created for user {UserId}",
            notification.Id, userId);

        return Result<Guid>.Success(notification.Id);
    }

    /// <inheritdoc/>
    public async Task<Result<int>> GetUnreadCountAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var count = await _db.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead, ct);

        return Result<int>.Success(count);
    }

    /// <inheritdoc/>
    public async Task<Result<bool>> MarkReadAsync(
        Guid notificationId,
        Guid userId,
        CancellationToken ct = default)
    {
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, ct);

        if (notification is null)
            return Result<bool>.Failure("Notification not found.", 404);

        if (notification.IsRead)
            return Result<bool>.Success(true); // idempotent

        notification.IsRead = true;
        notification.ReadAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(
            actorUserId: userId,
            action: "notification.read",
            entityType: "Notification",
            entityId: notificationId,
            ct: ct);

        return Result<bool>.Success(true);
    }

    /// <inheritdoc/>
    public async Task<Result<bool>> MarkAllReadAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var unread = await _db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync(ct);

        if (unread.Count == 0)
            return Result<bool>.Success(true); // idempotent

        var now = DateTime.UtcNow;
        foreach (var n in unread)
        {
            n.IsRead = true;
            n.ReadAt = now;
        }

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(
            actorUserId: userId,
            action: "notification.read_all",
            entityType: "Notification",
            entityId: null,
            details: $"marked {unread.Count} as read",
            ct: ct);

        return Result<bool>.Success(true);
    }

    /// <inheritdoc/>
    public async Task<Result<IEnumerable<NotificationDto>>> GetRecentAsync(
        Guid userId,
        int limit = 20,
        CancellationToken ct = default)
    {
        var capped = Math.Min(Math.Max(limit, 1), 100);

        var items = await _db.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(capped)
            .Select(n => new NotificationDto(
                n.Id,
                n.Title,
                n.Body,
                n.IsRead,
                n.ResourceType,
                n.ResourceId,
                n.CreatedAt))
            .ToListAsync(ct);

        return Result<IEnumerable<NotificationDto>>.Success(items);
    }
}
