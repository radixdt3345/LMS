using LMS.Application.DTOs.Reporting;
using LMS.Application.Interfaces;
using LMS.Domain.Common;
using LMS.Domain.Entities;
using LMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
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

    /// <inheritdoc />
    public async Task<Result<PagedResult<AuditLogDto>>> GetAuditLogsAsync(
        AuditLogQueryDto query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var limit = Math.Clamp(query.Limit, 1, 100);

        var q = _context.AuditLogs.AsNoTracking().AsQueryable();

        if (query.EntityType is not null)
            q = q.Where(l => l.EntityType == query.EntityType);

        if (query.EntityId.HasValue)
            q = q.Where(l => l.EntityId == query.EntityId.Value);

        if (query.ActorId.HasValue)
            q = q.Where(l => l.ActorId == query.ActorId.Value);

        if (query.From.HasValue)
            q = q.Where(l => l.CreatedAt >= query.From.Value);

        if (query.To.HasValue)
            q = q.Where(l => l.CreatedAt <= query.To.Value);

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(l => new AuditLogDto(
                l.Id,
                l.Action,
                l.EntityType,
                l.EntityId,
                l.ActorId,
                l.OldValue,
                l.NewValue,
                l.CreatedAt))
            .ToListAsync(ct);

        return Result<PagedResult<AuditLogDto>>.Success(new PagedResult<AuditLogDto>
        {
            Items = items,
            Total = total,
            Page = page,
            Limit = limit,
        });
    }
}
