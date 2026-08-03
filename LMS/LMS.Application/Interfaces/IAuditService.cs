namespace LMS.Application.Interfaces;

/// <summary>
/// Append-only audit log.
/// Every domain service calls LogAsync on every state-mutating operation.
/// The audit log must never be deleted (AuditService.Delete throws — UT-56, IT-50).
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Records an immutable audit entry.
    /// </summary>
    /// <param name="actorUserId">User who triggered the action (null for system-initiated events).</param>
    /// <param name="action">Dot-separated verb, e.g. "notification.created", "leave_request.approved".</param>
    /// <param name="entityType">Entity name, e.g. "Notification", "LeaveRequest".</param>
    /// <param name="entityId">PK of the affected entity (null for bulk operations).</param>
    /// <param name="details">Optional free-text context.</param>
    Task LogAsync(
        Guid? actorUserId,
        string action,
        string entityType,
        Guid? entityId,
        string? details = null,
        CancellationToken ct = default);
}
