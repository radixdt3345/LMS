namespace LMS.Application.Interfaces;

/// <summary>
/// Contract for the domain audit service.
/// All mutations in domain services must call <see cref="LogAsync"/> on state change.
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Writes an audit record to the database.
    /// This method must be called by every domain service on every state change.
    /// </summary>
    /// <param name="action">Verb describing the operation (e.g. "LeaveRequest.Submit").</param>
    /// <param name="entityType">Domain entity type name (e.g. "LeaveRequest").</param>
    /// <param name="entityId">UUID of the entity that changed.</param>
    /// <param name="actorId">UUID of the user who performed the action.</param>
    /// <param name="oldValue">Entity state before the change; null for creates.</param>
    /// <param name="newValue">Entity state after the change; null for deletes.</param>
    Task LogAsync(
        string action,
        string entityType,
        Guid entityId,
        Guid actorId,
        object? oldValue = null,
        object? newValue = null);

    /// <summary>
    /// This method MUST throw <see cref="InvalidOperationException"/>.
    /// Audit logs are immutable — deletion is never permitted (UT-56, IT-50).
    /// </summary>
    /// <exception cref="InvalidOperationException">Always thrown. No DB call is made.</exception>
    void Delete(Guid id);
}
