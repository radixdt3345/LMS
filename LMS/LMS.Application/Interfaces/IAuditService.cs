namespace LMS.Application.Interfaces;

/// <summary>
/// Audit service interface — records state-change events for domain mutations.
/// Full persistence implementation lives in the REPORTING domain issue.
/// Every service that mutates domain state must call LogAsync (CONSTITUTION Art II).
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Records an audit event. Fire-and-forget in production; awaited in tests.
    /// </summary>
    /// <param name="entityType">Domain entity name, e.g. "User", "LeaveRequest".</param>
    /// <param name="entityId">String representation of the entity PK (Guid.ToString()).</param>
    /// <param name="action">Verb: Create, Update, Deactivate, Approve, Reject, etc.</param>
    /// <param name="userId">Acting user's ID, or null when called by a background job/seed.</param>
    /// <param name="details">Human-readable summary for the audit log.</param>
    /// <param name="ct">Cancellation token.</param>
    Task LogAsync(
        string entityType,
        string entityId,
        string action,
        string? userId,
        string? details,
        CancellationToken ct = default);
}
