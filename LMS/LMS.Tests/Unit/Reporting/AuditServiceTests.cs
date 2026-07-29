using LMS.Infrastructure.Data;
using LMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace LMS.Tests.Unit.Reporting;

/// <summary>
/// Unit tests for AuditService.
/// Uses EF Core InMemory provider; no PostgreSQL required.
/// </summary>
[Trait("Category", "Unit")]
public class AuditServiceTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static LmsDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<LmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new LmsDbContext(options);
    }

    private static AuditService BuildService(LmsDbContext db)
    {
        var logger = new Mock<ILogger<AuditService>>();
        return new AuditService(db, logger.Object);
    }

    // ── UT-56: Delete always throws, no DB call ────────────────────────────────

    /// <summary>
    /// UT-56: AuditService.Delete(Guid) MUST throw InvalidOperationException
    /// and MUST NOT make any database call, regardless of the ID passed.
    /// Audit logs are append-only and immutable by design.
    /// </summary>
    [Fact]
    public void Delete_AlwaysThrowsInvalidOperationException_WithNoDatabaseCall()
    {
        // Arrange — fresh in-memory context (no rows)
        using var db = CreateInMemoryDb();
        var svc = BuildService(db);
        var arbitraryId = Guid.NewGuid();

        // Act & Assert — exception must be thrown unconditionally
        var ex = Assert.Throws<InvalidOperationException>(
            () => svc.Delete(arbitraryId));

        // Message must communicate immutability
        Assert.Contains("immutable", ex.Message, StringComparison.OrdinalIgnoreCase);

        // No DB call was made: AuditLogs table must still be empty
        Assert.Empty(db.AuditLogs);
    }

    /// <summary>
    /// UT-56 (variant): Delete throws even when rows exist in the table.
    /// The method must not query or touch the database regardless of state.
    /// </summary>
    [Fact]
    public async Task Delete_ThrowsEvenWhenAuditLogsExist_NoDbCallMade()
    {
        // Arrange — pre-populate via LogAsync (which IS allowed to write)
        using var db = CreateInMemoryDb();
        var svc = BuildService(db);
        var actorId = Guid.NewGuid();
        var entityId = Guid.NewGuid();

        // Seed one audit log via the legitimate write path
        await svc.LogAsync("Entity.Create", "TestEntity", entityId, actorId);
        Assert.Single(db.AuditLogs); // confirm the row exists

        // Act & Assert — Delete still throws; the existing row is untouched
        Assert.Throws<InvalidOperationException>(() => svc.Delete(entityId));
        Assert.Single(db.AuditLogs); // count unchanged — no delete occurred
    }
}
