using LMS.Domain.Entities;
using LMS.Infrastructure.Data;
using LMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LMS.Tests.Unit.Reporting;

[Trait("Category", "Unit")]
public class AuditServiceTests
{
    private static LmsDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<LmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new LmsDbContext(opts);
    }

    [Fact(DisplayName = "UT-56 (CRITICAL): AuditService.Delete throws InvalidOperationException — no DB call made")]
    public async Task Delete_AlwaysThrowsInvalidOperationException_NoDatabaseCallMade()
    {
        await using var db = CreateDb();
        var audit = new AuditService(db, NullLogger<AuditService>.Instance);

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(), Action = "SomeAction", EntityType = "SomeEntity",
            EntityId = Guid.NewGuid(), ActorId = Guid.NewGuid(), CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var ex = Assert.Throws<InvalidOperationException>(() => audit.Delete(Guid.NewGuid()));
        Assert.Contains("immutable", ex.Message, StringComparison.OrdinalIgnoreCase);

        db.ChangeTracker.Clear();
        var rowCount = await db.AuditLogs.CountAsync();
        Assert.Equal(1, rowCount);
    }
}
