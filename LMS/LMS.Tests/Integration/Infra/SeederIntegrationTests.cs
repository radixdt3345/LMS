using LMS.Domain.Enums;
using LMS.Infrastructure.Data;
using LMS.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LMS.Tests.Integration.Infra;

/// <summary>
/// Integration tests for INFRA-TEST-001: SeedService idempotent startup seeder.
///
/// IT-45 — SeedAsync on a fresh InMemory DB creates all expected seed rows.
/// IT-46 — SeedAsync called twice on the same DB produces no duplicate rows.
///
/// Run: dotnet test --filter Category=Integration
/// </summary>
[Trait("Category", "Integration")]
public class SeederIntegrationTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a fresh isolated InMemory database for each test.
    /// A unique database name guarantees tests cannot share state.
    /// </summary>
    private static LmsDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<LmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new LmsDbContext(opts);
    }

    /// <summary>
    /// Builds a <see cref="SeedService"/> against the supplied context.
    /// IConfiguration supplies the seeded email/password values;
    /// ILogger uses the no-op null logger so tests produce no console noise.
    /// </summary>
    private static SeedService CreateSeeder(LmsDbContext db)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seed:SuperAdminEmail"]    = "superadmin@lms.local",
                ["Seed:SuperAdminPassword"] = "SuperAdmin@123",
                ["Seed:HrAdminEmail"]       = "hradmin@lms.local",
                ["Seed:HrAdminPassword"]    = "HrAdmin@123",
            })
            .Build();

        return new SeedService(db, config, NullLogger<SeedService>.Instance);
    }

    // ── IT-45 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// IT-45: Running SeedAsync once on a completely empty InMemory DB must produce:
    ///   - At least 1 LeaveType row (Annual Leave, Sick Leave, …)
    ///   - At least 1 User with role SuperAdmin or HRAdmin
    ///   - At least 1 Department row (HR department)
    /// </summary>
    [Fact]
    public async Task IT45_SeedAsync_OnEmptyDb_CreatesExpectedRows()
    {
        // Arrange — brand-new empty database
        await using var db = CreateDb();
        var seeder = CreateSeeder(db);

        // Act
        await seeder.SeedAsync();

        // Assert — leave types (at least the 5 standard types)
        var leaveTypeCount = await db.LeaveTypes.CountAsync();
        Assert.True(
            leaveTypeCount >= 1,
            $"Expected at least 1 LeaveType row after seeding; found {leaveTypeCount}.");

        // Assert — admin user (SuperAdmin or HRAdmin must exist)
        var adminCount = await db.Users
            .Where(u => u.Role == UserRole.SuperAdmin || u.Role == UserRole.HRAdmin)
            .CountAsync();
        Assert.True(
            adminCount >= 1,
            $"Expected at least 1 admin (SuperAdmin / HRAdmin) user after seeding; found {adminCount}.");

        // Assert — HR department seeded
        var deptCount = await db.Departments.CountAsync();
        Assert.True(
            deptCount >= 1,
            $"Expected at least 1 Department row after seeding; found {deptCount}.");
    }

    // ── IT-46 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// IT-46: SeedAsync is idempotent — calling it twice on the same database
    /// must not produce any duplicate rows. Counts after the second call must
    /// equal counts after the first call for every seeded entity set.
    /// </summary>
    [Fact]
    public async Task IT46_SeedAsync_CalledTwice_ProducesNoDuplicates()
    {
        // Arrange — shared database for both calls
        await using var db = CreateDb();
        var seeder = CreateSeeder(db);

        // Act — first seed
        await seeder.SeedAsync();

        var leaveTypesAfterFirst  = await db.LeaveTypes.CountAsync();
        var usersAfterFirst       = await db.Users.CountAsync();
        var deptsAfterFirst       = await db.Departments.CountAsync();

        // Act — second seed on the same DB (idempotency check)
        await seeder.SeedAsync();

        var leaveTypesAfterSecond = await db.LeaveTypes.CountAsync();
        var usersAfterSecond      = await db.Users.CountAsync();
        var deptsAfterSecond      = await db.Departments.CountAsync();

        // Assert — no row counts changed on the second pass
        Assert.Equal(leaveTypesAfterFirst, leaveTypesAfterSecond);
        Assert.Equal(usersAfterFirst,      usersAfterSecond);
        Assert.Equal(deptsAfterFirst,      deptsAfterSecond);
    }
}
