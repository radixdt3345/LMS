using LMS.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace LMS.Infrastructure.Services;

/// <summary>
/// Stub audit service — logs to ILogger<AuditService> until the REPORTING domain
/// implements the full audit_log table (append-only, immutable per UT-56).
/// Replace this implementation — do not extend it — when the REPORTING issue lands.
/// </summary>
public class AuditService : IAuditService
{
    private readonly ILogger<AuditService> _logger;

    public AuditService(ILogger<AuditService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task LogAsync(
        string entityType,
        string entityId,
        string action,
        string? userId,
        string? details,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[AUDIT] {EntityType} {EntityId} — {Action} by {UserId}. {Details}",
            entityType, entityId, action, userId ?? "system", details);

        return Task.CompletedTask;
    }
}
