using LMS.Application.Interfaces;
using LMS.Domain.Entities;
using LMS.Infrastructure.Data;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace LMS.Infrastructure.Services;

/// <summary>
/// Real EF Core-backed implementation of <see cref="IAuditService"/>.
/// Every call to <see cref="LogAsync"/> inserts a row into <c>audit_logs</c>;
/// structured logging accompanies every write for observability.
/// <para>
/// <strong>Delete is permanently forbidden.</strong>
/// <see cref="Delete"/> throws <see cref="InvalidOperationException"/> and makes
/// no database call whatsoever (UT-56, IT-50).
/// </para>
/// </summary>
public class AuditService : IAuditService
{
    private readonly LmsDbContext _context;
    private readonly ILogger<AuditService> _logger;

    /// <summary>Initialises the service with a DB context and logger.</summary>
    public AuditService(LmsDbContext context, ILogger<AuditService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task LogAsync(
        string action,
        string entityType,
        Guid entityId,
        Guid actorId,
        object? oldValue = null,
        object? newValue = null)
    {
        var entry = new AuditLog
        {
            Id = Guid.NewGuid(),
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            ActorId = actorId,
            OldValue = oldValue is null ? null : JsonSerializer.Serialize(oldValue),
            NewValue = newValue is null ? null : JsonSerializer.Serialize(newValue),
            CreatedAt = DateTime.UtcNow
        };

        _context.AuditLogs.Add(entry);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Audit: {Action} on {EntityType}/{EntityId} by actor {ActorId} at {CreatedAt}",
            action, entityType, entityId, actorId, entry.CreatedAt);
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// Always thrown. Audit logs are immutable — delete is not permitted.
    /// No database call is made.
    /// </exception>
    public void Delete(Guid id)
    {
        // CRITICAL: NO database call is permitted here (UT-56, IT-50).
        // This throw is unconditional and intentional.
        throw new InvalidOperationException(
            "Audit log is immutable — delete is not permitted");
    }
}
