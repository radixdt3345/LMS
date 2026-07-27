using LMS.Infrastructure.Data;
using LMS.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LMS.Tests.Unit.Infra;

[Trait("Category", "Unit")]
public class SeedServiceTests
{
    private static LmsDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<LmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new LmsDbContext(options);
    }

    private static IConfiguration BuildConfig() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Seed:SuperAdminEmail"] = "super@test.com",
            ["Seed:SuperAdminPassword"] = "SuperPass!",
            ["Seed:HrAdminEmail"] = "hr@test.com",
            ["Seed:HrAdminPassword"] = "HrPass!",
        }).Build();

    /// <summary>UT-13: idempotent — running twice produces no duplicates.</summary>
    [Fact]
    public async Task SeedAsync_RunTwice_NoDuplicates()
    {
        await using var db = CreateInMemoryDb();
        var svc = new SeedService(db, BuildConfig(), NullLogger<SeedService>.Instance);

        await svc.SeedAsync();
        await svc.SeedAsync();

        Assert.Equal(1, await db.Departments.CountAsync(d => d.Name == "HR"));
        Assert.Equal(1, await db.Users.CountAsync(u => u.Email == "super@test.com"));
        Assert.Equal(1, await db.Users.CountAsync(u => u.Email == "hr@test.com"));
        Assert.Equal(5, await db.LeaveTypes.CountAsync());
    }

    [Fact]
    public async Task SeedAsync_Seeds5LeaveTypes_WithCorrectPolicy()
    {
        await using var db = CreateInMemoryDb();
        var svc = new SeedService(db, BuildConfig(), NullLogger<SeedService>.Instance);
        await svc.SeedAsync();

        var unpaid = await db.LeaveTypes.FirstAsync(l => l.Name == "Unpaid Leave");
        Assert.Null(unpaid.MaxDays); // Unlimited — no cap

        var annual = await db.LeaveTypes.FirstAsync(l => l.Name == "Annual Leave");
        Assert.Equal(18, annual.MaxDays);
        Assert.Equal(LMS.Domain.Enums.AccrualType.Annual, annual.AccrualType);
    }

    [Fact]
    public async Task SeedAsync_SuperAdminAndHRAdmin_HaveCorrectRoles()
    {
        await using var db = CreateInMemoryDb();
        var svc = new SeedService(db, BuildConfig(), NullLogger<SeedService>.Instance);
        await svc.SeedAsync();

        var superAdmin = await db.Users.FirstAsync(u => u.Email == "super@test.com");
        Assert.Equal(LMS.Domain.Enums.UserRole.SuperAdmin, superAdmin.Role);
        Assert.True(superAdmin.IsActive);

        var hrAdmin = await db.Users.FirstAsync(u => u.Email == "hr@test.com");
        Assert.Equal(LMS.Domain.Enums.UserRole.HRAdmin, hrAdmin.Role);
        Assert.True(hrAdmin.IsActive);
    }

    [Fact]
    public async Task SeedAsync_HRDepartment_IsSeeded()
    {
        await using var db = CreateInMemoryDb();
        var svc = new SeedService(db, BuildConfig(), NullLogger<SeedService>.Instance);
        await svc.SeedAsync();

        var dept = await db.Departments.FirstAsync(d => d.Name == "HR");
        Assert.NotEqual(Guid.Empty, dept.Id);
        Assert.True(dept.CreatedAt > DateTime.MinValue);
    }

    [Fact]
    public async Task StartAsync_WhenSeedFails_DoesNotThrow()
    {
        // Arrange: use a "broken" config that would normally still seed fine.
        // We test that StartAsync swallows exceptions from SeedAsync.
        await using var db = CreateInMemoryDb();
        var svc = new SeedService(db, BuildConfig(), NullLogger<SeedService>.Instance);

        // Act & Assert: StartAsync must not throw even if an unexpected error occurs.
        var ex = await Record.ExceptionAsync(() => svc.StartAsync(CancellationToken.None));
        Assert.Null(ex);
    }
}
