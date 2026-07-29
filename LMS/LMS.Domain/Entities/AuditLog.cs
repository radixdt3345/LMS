namespace LMS.Domain.Entities;

/// <summary>
/// An append-only audit record capturing every domain state change.
/// <para>
/// <strong>INVARIANT:</strong> Audit logs are NEVER updated or deleted.
/// <c>AuditService.Delete</c> throws <see cref="InvalidOperationException"/> unconditionally.
/// </para>
/// </summary>
public class AuditLog
{
    /// <summary>Surrogate primary key (UUID).</summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The action performed (e.g. "LeaveRequest.Submit", "LeaveRequest.Approve").
    /// Max 100 chars.
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// The domain entity type that changed (e.g. "LeaveRequest", "CompOffRequest").
    /// Max 100 chars.
    /// </summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>UUID of the entity that changed.</summary>
    public Guid EntityId { get; set; }

    /// <summary>FK to the user who performed the action.</summary>
    public Guid ActorId { get; set; }

    /// <summary>
    /// JSON-serialized state of the entity before the change.
    /// Null for creation events.
    /// Stored as jsonb in PostgreSQL.
    /// </summary>
    public string? OldValue { get; set; }

    /// <summary>
    /// JSON-serialized state of the entity after the change.
    /// Null for deletion events.
    /// Stored as jsonb in PostgreSQL.
    /// </summary>
    public string? NewValue { get; set; }

    /// <summary>UTC timestamp when this audit record was created.</summary>
    public DateTime CreatedAt { get; set; }
}
