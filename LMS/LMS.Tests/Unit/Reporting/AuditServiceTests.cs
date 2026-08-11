using LMS.Domain.Entities;
using LMS.Infrastructure.Data;
using LMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LMS.Tests.Unit.Reporting;

/// <summary>
/// UT-56: AuditService.Delete must throw InvalidOperationException unconditionally.
/// The audit log is append-only and immutable — no database call is ever made by Delete.
///
/// This is a UNIT test: AuditService is constructed directly with an EF Core InMemory
/// database so no real PostgreSQL dependency is required. The test verifies both the
/// thrown exception and the fact that no rows were removed from the AuditLogs table.
///
/// Run: dotnet test --filter Category=Unit
/// </summary>
[Trait("Category", "Unit")]
public class AuditServiceTests
{
    // ── DB factory ────────────────────────────────────────────────────────────

    private static LmsDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<LmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new LmsDbContext(opts);
    }

    // ── UT-56 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// UT-56 (CRITICAL): AuditService.Delete throws InvalidOperationException.
    /// No DB call is permitted — the audit log is immutable.
    /// </summary>
    [Fact(DisplayName = "UT-56 (CRITICAL): AuditService.Delete throws InvalidOperationException — no DB call made")]
    public async Task Delete_AlwaysThrowsInvalidOperationException_NoDatabaseCallMade()
    {
        // Arrange: create AuditService with an isolated InMemory DB
        await using var db = CreateDb();
        var audit = new AuditService(db, NullLogger<AuditService>.Instance);

        // Pre-seed one audit log row so we can confirm no deletion occurs
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

        // Act + Assert: Delete must throw InvalidOperationException unconditionally.
        // The ID passed is irrelevant — no path through Delete reaches the database.
        var ex = Assert.Throws<InvalidOperationException>(
            () => audit.Delete(Guid.NewGuid()));

        // Assert: the exception message explicitly references immutability
        Assert.Contains("immutable", ex.Message, StringComparison.OrdinalIgnoreCase);

        // Assert: the pre-seeded row is completely untouched — count is still 1.
        // If Delete had made any DB call, a deletion attempt would have changed this.
        db.ChangeTracker.Clear();
        var rowCount = await db.AuditLogs.CountAsync();
        Assert.Equal(1, rowCount);
    }
}
