namespace LMS.Application.DTOs.Reporting;

/// <summary>
/// Read model for a single audit log entry returned by GET /api/v1/audit-logs.
/// </summary>
/// <param name="Id">Surrogate primary key.</param>
/// <param name="Action">The operation performed (e.g. "LeaveRequest.Submit").</param>
/// <param name="EntityType">Domain entity type that changed (e.g. "LeaveRequest").</param>
/// <param name="EntityId">UUID of the entity that changed.</param>
/// <param name="ActorId">UUID of the user who performed the action.</param>
/// <param name="OldValue">JSON-serialised entity state before change; null for creates.</param>
/// <param name="NewValue">JSON-serialised entity state after change; null for deletes.</param>
/// <param name="CreatedAt">UTC timestamp of the audit event.</param>
public record AuditLogDto(
    Guid Id,
    string Action,
    string EntityType,
    Guid EntityId,
    Guid ActorId,
    string? OldValue,
    string? NewValue,
    DateTime CreatedAt);

/// <summary>
/// Query parameters for GET /api/v1/audit-logs.
/// All filter fields are optional; omitted fields are not applied.
/// Page is 1-based. Limit is clamped to 1–100 by the service.
/// </summary>
public class AuditLogQueryDto
{
    /// <summary>Filter by entity type (exact match).</summary>
    public string? EntityType { get; set; }

    /// <summary>Filter by entity UUID (exact match).</summary>
    public Guid? EntityId { get; set; }

    /// <summary>Filter by actor UUID (exact match).</summary>
    public Guid? ActorId { get; set; }

    /// <summary>Inclusive lower bound on CreatedAt (UTC).</summary>
    public DateTime? From { get; set; }

    /// <summary>Inclusive upper bound on CreatedAt (UTC).</summary>
    public DateTime? To { get; set; }

    /// <summary>1-based page number. Defaults to 1.</summary>
    public int Page { get; set; } = 1;

    /// <summary>Page size. Defaults to 20; clamped to 100 by the service.</summary>
    public int Limit { get; set; } = 20;
}
