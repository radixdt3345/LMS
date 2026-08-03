using LMS.Application.DTOs.Notification;
using LMS.Domain.Common;

namespace LMS.Application.Interfaces;

/// <summary>
/// In-app notification operations.
/// All write operations call AuditService.LogAsync.
/// </summary>
public interface INotificationService
{
    Task<Result<Guid>> CreateNotificationAsync(
        Guid userId,
        string title,
        string body,
        string? resourceType = null,
        Guid? resourceId = null,
        CancellationToken ct = default);

    Task<Result<int>> GetUnreadCountAsync(Guid userId, CancellationToken ct = default);

    Task<Result<bool>> MarkReadAsync(Guid notificationId, Guid userId, CancellationToken ct = default);

    Task<Result<bool>> MarkAllReadAsync(Guid userId, CancellationToken ct = default);

    Task<Result<IEnumerable<NotificationDto>>> GetRecentAsync(
        Guid userId,
        int limit = 20,
        CancellationToken ct = default);
}
