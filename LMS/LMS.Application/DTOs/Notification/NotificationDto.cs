namespace LMS.Application.DTOs.Notification;

/// <summary>
/// Read-only projection of a Notification entity returned by the API.
/// All timestamps are UTC.
/// </summary>
public record NotificationDto(
    Guid Id,
    string Title,
    string Body,
    bool IsRead,
    string? ResourceType,
    Guid? ResourceId,
    DateTime CreatedAt);
