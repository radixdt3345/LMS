using LMS.Application.Interfaces;
using LMS.Domain.Entities;
using LMS.Domain.Enums;
using LMS.Infrastructure.Data;
using LMS.Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace LMS.Tests.Integration.LeaveCore;

/// <summary>
/// Integration tests for LEAVECORE-INT-002: ILeaveBalanceService balance operations.
///
/// IT-17 — CreditAnnual creates a balance row with correct allocated_days.
/// IT-18 — DeductBalance / RestoreBalance round-trip produces exact decimal result.
///
/// Run: dotnet test --filter Category=Integration
/// </summary>
[Trait("Category", "Integration")]
public class LeaveBalanceIntegrationTests : IClassFixture<LeaveBalanceIntegrationFactory>
{
    private readonly LeaveBalanceIntegrationFactory _factory;

    public LeaveBalanceIntegrationTests(LeaveBalanceIntegrationFactory factory)
    {
        _factory = factory;
    }

    // ---- helpers ------------------------------------------------------------

    /// <summary>
    /// Creates a fresh isolated InMemory database for each test.
    /// </summary>
    private static LmsDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<LmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new LmsDbContext(opts);
    }

    private static User MakeActiveUser() => new()
    {
        Id        = Guid.NewGuid(),
        Email     = $"{Guid.NewGuid():N}@test.local",
        IsActive  = true,
        Role      = UserRole.Employee,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    private static LeaveType MakeAnnualLeaveType(int maxDays) => new()
    {
        Id             = Guid.NewGuid(),
        Name           = "Annual Leave",
        AccrualType    = AccrualType.Annual,
        MaxDaysPerYear = maxDays,
        IsActive       = true,
        CreatedAt      = DateTime.UtcNow,
        UpdatedAt      = DateTime.UtcNow,
    };

    // ---- IT-17: CreditAnnual creates balance row with correct allocated_days ---

    /// <summary>
    /// IT-17: CreditAnnual(year) must create exactly one LeaveBalance row per
    /// active-user x active-leave-type combination, with AllocatedDays equal to
    /// the leave type's MaxDaysPerYear and UsedDays initialised to zero.
    /// </summary>
    [Fact]
    public async Task IT17_CreditAnnual_CreatesBalanceRowWithCorrectAllocatedDays()
    {
        // Arrange
        await using var db = CreateDb();
        var user      = MakeActiveUser();
        var leaveType = MakeAnnualLeaveType(maxDays: 10);

        db.Users.Add(user);
        db.LeaveTypes.Add(leaveType);
        await db.SaveChangesAsync();

        var svc  = new LeaveBalanceService(db);
        var year = DateTime.UtcNow.Year;

        // Act
        await svc.CreditAnnual(year);

        // Assert — exactly one balance row must exist
        var balances = await db.LeaveBalances.ToListAsync();
        Assert.Single(balances);

        var balance = balances[0];
        Assert.Equal(user.Id,       balance.UserId);
        Assert.Equal(leaveType.Id,  balance.LeaveTypeId);
        Assert.Equal((short)year,   balance.Year);
        Assert.Equal(10m,           balance.AllocatedDays);
        Assert.Equal(0m,            balance.UsedDays);
    }

    /// <summary>
    /// IT-17 (idempotent upsert): calling CreditAnnual twice must not duplicate rows;
    /// the second call updates AllocatedDays in place.
    /// </summary>
    [Fact]
    public async Task IT17_CreditAnnual_IsIdempotent_NoDoubleRows()
    {
        await using var db = CreateDb();
        db.Users.Add(MakeActiveUser());
        db.LeaveTypes.Add(MakeAnnualLeaveType(maxDays: 21));
        await db.SaveChangesAsync();

        var svc  = new LeaveBalanceService(db);
        var year = DateTime.UtcNow.Year;

        await svc.CreditAnnual(year);
        await svc.CreditAnnual(year); // second call — must upsert, not insert

        var count = await db.LeaveBalances.CountAsync();
        Assert.Equal(1, count);
    }

    // ---- IT-18: DeductBalance / RestoreBalance round-trip -------------------

    /// <summary>
    /// IT-18a: DeductBalance(3d) reduces UsedDays by 3, leaving 7 available.
    /// IT-18b: RestoreBalance(3d) brings UsedDays back to 0 and available to
    ///         exactly 10.0m — no floating-point rounding error (decimal arithmetic).
    /// </summary>
    [Fact]
    public async Task IT18_DeductThenRestoreBalance_ExactDecimalResult()
    {
        // Arrange
        await using var db = CreateDb();
        var user      = MakeActiveUser();
        var leaveType = MakeAnnualLeaveType(maxDays: 10);
        var year      = (short)DateTime.UtcNow.Year;

        db.Users.Add(user);
        db.LeaveTypes.Add(leaveType);
        db.LeaveBalances.Add(new LeaveBalance
        {
            Id            = Guid.NewGuid(),
            UserId        = user.Id,
            LeaveTypeId   = leaveType.Id,
            Year          = year,
            AllocatedDays = 10m,
            UsedDays      = 0m,
            CreatedAt     = DateTime.UtcNow,
            UpdatedAt     = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var svc = new LeaveBalanceService(db);

        // Act 1 — deduct 3 days
        var deductResult = await svc.DeductBalance(user.Id, leaveType.Id, 3m);

        // Assert after deduct
        Assert.True(deductResult.IsSuccess,
            $"DeductBalance failed: {deductResult.Error}");

        var afterDeduct = await db.LeaveBalances
            .AsNoTracking()
            .FirstAsync(b => b.UserId == user.Id && b.LeaveTypeId == leaveType.Id);
        Assert.Equal(3m,  afterDeduct.UsedDays);
        Assert.Equal(7m,  afterDeduct.AllocatedDays - afterDeduct.UsedDays); // 7 remaining

        // Act 2 — restore 3 days
        await svc.RestoreBalance(user.Id, leaveType.Id, 3m);

        // Assert after restore — UsedDays must be exactly 0m (no rounding drift)
        var afterRestore = await db.LeaveBalances
            .AsNoTracking()
            .FirstAsync(b => b.UserId == user.Id && b.LeaveTypeId == leaveType.Id);
        Assert.Equal(0m,    afterRestore.UsedDays);
        Assert.Equal(10.0m, afterRestore.AllocatedDays - afterRestore.UsedDays);
    }

    /// <summary>
    /// IT-18 (clamp): RestoreBalance must never produce negative UsedDays
    /// even if days restored exceed days used.
    /// </summary>
    [Fact]
    public async Task IT18_RestoreBalance_ClampsAtZero_NoNegativeUsedDays()
    {
        await using var db = CreateDb();
        var user      = MakeActiveUser();
        var leaveType = MakeAnnualLeaveType(maxDays: 10);
        var year      = (short)DateTime.UtcNow.Year;

        db.Users.Add(user);
        db.LeaveTypes.Add(leaveType);
        db.LeaveBalances.Add(new LeaveBalance
        {
            Id            = Guid.NewGuid(),
            UserId        = user.Id,
            LeaveTypeId   = leaveType.Id,
            Year          = year,
            AllocatedDays = 10m,
            UsedDays      = 2m,
            CreatedAt     = DateTime.UtcNow,
            UpdatedAt     = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var svc = new LeaveBalanceService(db);

        // Restore 5 days when only 2 were used — clamp must kick in.
        await svc.RestoreBalance(user.Id, leaveType.Id, 5m);

        var balance = await db.LeaveBalances
            .AsNoTracking()
            .FirstAsync(b => b.UserId == user.Id);
        Assert.Equal(0m, balance.UsedDays); // clamped, not -3
    }
}

/// <summary>
/// WebApplicationFactory for LeaveBalance integration tests.
/// Inherits CustomWebApplicationFactory (InMemory DB + no SeedService)
/// and additionally strips Hangfire hosted services.
/// </summary>
public class LeaveBalanceIntegrationFactory : CustomWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            // Remove Hangfire background server — it requires a live PostgreSQL
            // connection which is not available during integration tests.
            var hangfireHosted = services
                .Where(d =>
                    d.ServiceType == typeof(IHostedService) &&
                    d.ImplementationType is not null &&
                    d.ImplementationType.FullName != null &&
                    d.ImplementationType.FullName.Contains("Hangfire"))
                .ToList();
            foreach (var d in hangfireHosted)
                services.Remove(d);
        });
    }
}
