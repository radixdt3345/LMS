using LMS.Application.DTOs.Reporting;
using LMS.Domain.Entities;
using LMS.Domain.Enums;
using LMS.Infrastructure.Data;
using LMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LMS.Tests.Integration.Reporting;

/// <summary>
/// IT-49 to IT-51: AuditService integration tests against EF Core InMemory.
///
/// IT-49: AuditService.LogAsync inserts a row into audit_logs;
///        the row carries the correct action, entity_type, and entity_id.
/// IT-50 (CRITICAL): AuditService.Delete unconditionally throws InvalidOperationException.
///        No database call is made — the audit log is append-only and immutable (UT-56, IT-50).
/// IT-51: AuditService.GetAuditLogsAsync with entity_type=Employee filter
///        returns only Employee entries; LeaveRequest entries are excluded.
///
/// Run: dotnet test --filter Category=Integration
/// </summary>
[Trait("Category", "Integration")]
public class AuditIntegrationTests
{
    // ── DB factory ────────────────────────────────────────────────────────────

    private static LmsDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<LmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new LmsDbContext(opts);
    }

    // ── IT-49 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// IT-49: Calling AuditService.LogAsync with action="EmployeeUpdated",
    /// entity_type="Employee", entity_id=employeeId persists exactly one
    /// audit_log row with those values.
    /// </summary>
    [Fact(DisplayName = "IT-49: AuditService.LogAsync persists row with correct action, entity_type, entity_id")]
    public async Task IT49_LogAsync_PersistsAuditRow_WithCorrectActionEntityTypeEntityId()
    {
        await using var db = CreateDb();
        var audit = new AuditService(db, NullLogger<AuditService>.Instance);

        // Seed an actor and an employee to give the audit log meaningful IDs
        var actor = new User
        {
            Id        = Guid.NewGuid(),
            Email     = $"actor-{Guid.NewGuid():N}@test.com",
            Role      = UserRole.HRAdmin,
            IsActive  = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        var employee = new User
        {
            Id        = Guid.NewGuid(),
            Email     = $"emp-{Guid.NewGuid():N}@test.com",
            Role      = UserRole.Employee,
            IsActive  = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Users.AddRange(actor, employee);
        await db.SaveChangesAsync();

        var employeeId = employee.Id;

        // Act — log an EmployeeUpdated audit event
        await audit.LogAsync(
            action:     "EmployeeUpdated",
            entityType: "Employee",
            entityId:   employeeId,
            actorId:    actor.Id);

        // Assert — exactly one audit_log row with the correct fields
        db.ChangeTracker.Clear();
        var log = await db.AuditLogs
            .FirstAsync(l => l.EntityId == employeeId);

        Assert.Equal("EmployeeUpdated", log.Action);
        Assert.Equal("Employee",         log.EntityType);
        Assert.Equal(employeeId,         log.EntityId);
        Assert.Equal(actor.Id,           log.ActorId);
    }

    // ── IT-50 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// IT-50 (CRITICAL): AuditService.Delete unconditionally throws InvalidOperationException.
    /// No DB rows are deleted — the audit log is immutable (UT-56).
    /// </summary>
    [Fact(DisplayName = "IT-50 (CRITICAL): AuditService.Delete throws InvalidOperationException — no DB rows deleted")]
    public async Task IT50_Delete_ThrowsInvalidOperationException_NoDbRowsDeleted()
    {
        await using var db = CreateDb();
        var audit = new AuditService(db, NullLogger<AuditService>.Instance);

        // Pre-seed one audit log row so we can verify it survives the Delete call
        db.AuditLogs.Add(new AuditLog
        {
            Id         = Guid.NewGuid(),
            Action     = "SomeAction",
            EntityType = "SomeEntity",
            EntityId   = Guid.NewGuid(),
            ActorId    = Guid.NewGuid(),
            CreatedAt  = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        // Act + Assert — Delete must throw unconditionally, regardless of the ID passed
        var ex = Assert.Throws<InvalidOperationException>(
            () => audit.Delete(Guid.NewGuid()));

        Assert.Contains("immutable", ex.Message, StringComparison.OrdinalIgnoreCase);

        // Assert — the pre-seeded row is completely untouched (no DB call was made)
        db.ChangeTracker.Clear();
        var count = await db.AuditLogs.CountAsync();
        Assert.Equal(1, count);
    }

    // ── IT-51 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// IT-51: Log 3 audit entries — entity_type "Employee", "LeaveRequest", "Employee".
    /// GetAuditLogsAsync with entity_type=Employee filter must return exactly 2 entries,
    /// all with EntityType="Employee". The LeaveRequest entry must be excluded.
    /// </summary>
    [Fact(DisplayName = "IT-51: GetAuditLogsAsync with entity_type=Employee filter returns only Employee entries")]
    public async Task IT51_GetAuditLogsAsync_EntityTypeFilter_ReturnsOnlyMatchingEntries()
    {
        await using var db = CreateDb();
        var audit = new AuditService(db, NullLogger<AuditService>.Instance);

        var actorId    = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var leaveReqId = Guid.NewGuid();

        // Log 3 entries: Employee, LeaveRequest, Employee
        await audit.LogAsync("Employee.Updated",       "Employee",     employeeId,  actorId);
        await audit.LogAsync("LeaveRequest.Submitted", "LeaveRequest", leaveReqId,  actorId);
        await audit.LogAsync("Employee.Deactivated",   "Employee",     employeeId,  actorId);

        // Act — query with entity_type=Employee filter
        var result = await audit.GetAuditLogsAsync(new AuditLogQueryDto
        {
            EntityType = "Employee",
            Page       = 1,
            Limit      = 20,
        });

        // Assert — exactly 2 Employee entries returned; LeaveRequest entry excluded
        Assert.True(result.IsSuccess);
        var items = result.Value!.Items.ToList();
        Assert.Equal(2, items.Count);
        Assert.All(items, item => Assert.Equal("Employee", item.EntityType));
        Assert.DoesNotContain(items, item => item.EntityType == "LeaveRequest");
        Assert.Equal(2, result.Value.Total);
    }
}
