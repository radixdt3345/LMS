using LMS.Domain.Entities;
using LMS.Domain.Enums;
using LMS.Infrastructure.Data;
using LMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LMS.Tests.Unit.LeaveCore;

/// <summary>
/// Unit tests for <see cref="LeaveBalanceService"/>.
/// Covers UT-22 through UT-31 using the EF Core InMemory provider.
/// Run: dotnet test --filter Category=Unit
/// </summary>
[Trait("Category", "Unit")]
public class LeaveBalanceServiceTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static LmsDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<LmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new LmsDbContext(options);
    }

    private static User MakeUser(bool isActive = true) => new()
    {
        Id        = Guid.NewGuid(),
        Email     = $"u-{Guid.NewGuid()}@test.com",
        Role      = UserRole.Employee,
        IsActive  = isActive,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    private static LeaveType MakeLeaveType(
        AccrualType accrualType = AccrualType.Annual,
        int? maxDays = 12) => new()
    {
        Id             = Guid.NewGuid(),
        Name           = $"LT-{Guid.NewGuid()}",
        AccrualType    = accrualType,
        MaxDaysPerYear = maxDays,
        IsActive       = true,
        CreatedAt      = DateTime.UtcNow,
        UpdatedAt      = DateTime.UtcNow,
    };

    private static LeaveBalance MakeBalance(
        Guid userId, Guid leaveTypeId,
        decimal allocated, decimal used,
        int year = 2025) => new()
    {
        Id            = Guid.NewGuid(),
        UserId        = userId,
        LeaveTypeId   = leaveTypeId,
        Year          = (short)year,
        AllocatedDays = allocated,
        UsedDays      = used,
        CreatedAt     = DateTime.UtcNow,
        UpdatedAt     = DateTime.UtcNow,
    };

    // ── UT-22: CreditAnnual allocates correct days per leave type entitlement ─

    [Fact]
    public async Task UT22_CreditAnnual_CreatesBalanceWithCorrectAllocatedDays()
    {
        await using var db  = CreateDb();
        var user            = MakeUser();
        var lt              = MakeLeaveType(AccrualType.Annual, maxDays: 15);
        db.Users.Add(user);
        db.LeaveTypes.Add(lt);
        await db.SaveChangesAsync();

        var svc = new LeaveBalanceService(db);
        await svc.CreditAnnual(2025);

        var balance = await db.LeaveBalances
            .FirstOrDefaultAsync(b => b.UserId == user.Id && b.Year == 2025);

        Assert.NotNull(balance);
        Assert.Equal(15m, balance.AllocatedDays);
        Assert.Equal(0m,  balance.UsedDays);
    }

    // ── UT-23: DeductBalance reduces used_days correctly ─────────────────────

    [Fact]
    public async Task UT23_DeductBalance_ReducesUsedDays()
    {
        await using var db = CreateDb();
        var user           = MakeUser();
        var lt             = MakeLeaveType(AccrualType.Annual, maxDays: 12);
        db.Users.Add(user);
        db.LeaveTypes.Add(lt);
        db.LeaveBalances.Add(
            MakeBalance(user.Id, lt.Id, allocated: 10m, used: 3m,
                        year: DateTime.UtcNow.Year));
        await db.SaveChangesAsync();

        var svc    = new LeaveBalanceService(db);
        var result = await svc.DeductBalance(user.Id, lt.Id, days: 2m);

        Assert.True(result.IsSuccess);
        var balance = await db.LeaveBalances.FirstAsync();
        Assert.Equal(5m, balance.UsedDays);
    }

    // ── UT-24: RestoreBalance reduces used_days (net restore) ─────────────────

    [Fact]
    public async Task UT24_RestoreBalance_DecreasesUsedDays()
    {
        await using var db = CreateDb();
        var user           = MakeUser();
        var lt             = MakeLeaveType(AccrualType.Annual, maxDays: 12);
        db.Users.Add(user);
        db.LeaveTypes.Add(lt);
        db.LeaveBalances.Add(
            MakeBalance(user.Id, lt.Id, allocated: 10m, used: 5m,
                        year: DateTime.UtcNow.Year));
        await db.SaveChangesAsync();

        var svc = new LeaveBalanceService(db);
        await svc.RestoreBalance(user.Id, lt.Id, days: 2m);

        var balance = await db.LeaveBalances.FirstAsync();
        Assert.Equal(3m, balance.UsedDays);
    }

    // ── UT-25: ProrateForNewJoiner mid-year formula (July = 6/12 months) ──────

    [Fact]
    public async Task UT25_ProrateForNewJoiner_JulyJoin_Yields6Months()
    {
        await using var db = CreateDb();
        var user           = MakeUser();
        var lt             = MakeLeaveType(AccrualType.Annual, maxDays: 12);
        db.Users.Add(user);
        db.LeaveTypes.Add(lt);
        await db.SaveChangesAsync();

        var svc = new LeaveBalanceService(db);
        // Join date: 1 July 2025 → remainingMonths = 12-7+1 = 6
        // credit = Round(6/12.0 * 12, 1, MidpointRounding.ToEven) = Round(6.0) = 6.0
        await svc.ProrateForNewJoiner(user.Id, new DateTime(2025, 7, 1));

        var balance = await db.LeaveBalances
            .FirstOrDefaultAsync(b => b.UserId == user.Id && b.Year == 2025);

        Assert.NotNull(balance);
        Assert.Equal(6.0m, balance.AllocatedDays);
    }

    // ── UT-26: Unpaid Leave (Unlimited AccrualType) bypasses balance check ────

    [Fact]
    public async Task UT26_DeductBalance_UnlimitedType_ZeroBalance_ReturnsSuccess()
    {
        await using var db = CreateDb();
        var user           = MakeUser();
        // Unlimited leave type — no MaxDaysPerYear
        var lt             = MakeLeaveType(AccrualType.Unlimited, maxDays: null);
        db.Users.Add(user);
        db.LeaveTypes.Add(lt);
        // No LeaveBalance row — zero balance on file
        await db.SaveChangesAsync();

        var svc    = new LeaveBalanceService(db);
        var result = await svc.DeductBalance(user.Id, lt.Id, days: 3m);

        // Must succeed despite zero balance — UT-26
        Assert.True(result.IsSuccess);
    }

    // ── UT-27: Insufficient balance returns Failure for non-Unlimited type ────

    [Fact]
    public async Task UT27_DeductBalance_InsufficientBalance_ReturnsFailure()
    {
        await using var db = CreateDb();
        var user           = MakeUser();
        var lt             = MakeLeaveType(AccrualType.Annual, maxDays: 5);
        db.Users.Add(user);
        db.LeaveTypes.Add(lt);
        // allocated=5, used=4 → available=1 → requesting 2 should fail
        db.LeaveBalances.Add(
            MakeBalance(user.Id, lt.Id, allocated: 5m, used: 4m,
                        year: DateTime.UtcNow.Year));
        await db.SaveChangesAsync();

        var svc    = new LeaveBalanceService(db);
        var result = await svc.DeductBalance(user.Id, lt.Id, days: 2m);

        Assert.False(result.IsSuccess);
        Assert.Contains("Insufficient", result.Error);
    }

    // ── UT-28: YearEndLapse zeroes all Annual and OneTime type balances ────────

    [Fact]
    public async Task UT28_YearEndLapse_ZeroesAnnualAndOneTimeBalances()
    {
        await using var db = CreateDb();
        var user           = MakeUser();
        var annual         = MakeLeaveType(AccrualType.Annual,  maxDays: 12);
        var oneTime        = MakeLeaveType(AccrualType.OneTime, maxDays: 90);
        var unlimited      = MakeLeaveType(AccrualType.Unlimited, maxDays: null);
        db.Users.Add(user);
        db.LeaveTypes.AddRange(annual, oneTime, unlimited);
        db.LeaveBalances.AddRange(
            MakeBalance(user.Id, annual.Id,    allocated: 12m,  used: 5m,  year: 2025),
            MakeBalance(user.Id, oneTime.Id,   allocated: 90m,  used: 30m, year: 2025),
            MakeBalance(user.Id, unlimited.Id, allocated: 0m,   used: 0m,  year: 2025));
        await db.SaveChangesAsync();

        var svc = new LeaveBalanceService(db);
        await svc.YearEndLapse(2025);

        var annualBal  = await db.LeaveBalances.FirstAsync(b => b.LeaveTypeId == annual.Id);
        var oneTimeBal = await db.LeaveBalances.FirstAsync(b => b.LeaveTypeId == oneTime.Id);
        var unlimBal   = await db.LeaveBalances.FirstAsync(b => b.LeaveTypeId == unlimited.Id);

        // Annual and OneTime must be zeroed — UT-28
        Assert.Equal(0m, annualBal.AllocatedDays);
        Assert.Equal(0m, annualBal.UsedDays);
        Assert.Equal(0m, oneTimeBal.AllocatedDays);
        Assert.Equal(0m, oneTimeBal.UsedDays);

        // Unlimited leave balance is untouched
        Assert.Equal(0m, unlimBal.AllocatedDays);
        Assert.Equal(0m, unlimBal.UsedDays);
    }

    // ── UT-29: NewYearCredit creates / upserts one row per active employee ────

    [Fact]
    public async Task UT29_CreditAnnual_CreatesRowForEachActiveEmployee()
    {
        await using var db  = CreateDb();
        var active1         = MakeUser(isActive: true);
        var active2         = MakeUser(isActive: true);
        var inactive        = MakeUser(isActive: false);
        var lt              = MakeLeaveType(AccrualType.Annual, maxDays: 12);
        db.Users.AddRange(active1, active2, inactive);
        db.LeaveTypes.Add(lt);
        await db.SaveChangesAsync();

        var svc = new LeaveBalanceService(db);
        await svc.CreditAnnual(2026);

        // Only 2 rows — inactive employee must NOT receive credit
        var rows = await db.LeaveBalances
            .Where(b => b.Year == 2026)
            .ToListAsync();

        Assert.Equal(2, rows.Count);
        Assert.DoesNotContain(rows, r => r.UserId == inactive.Id);
    }

    // ── UT-30: CompOffExpiry — expired credits get marked as used ─────────────

    [Fact]
    public async Task UT30_ExpireCompOffCredits_MarksExpiredCreditsAsFullyUsed()
    {
        await using var db = CreateDb();
        var user           = MakeUser();
        db.Users.Add(user);

        // We need a CompOffRequest placeholder — use a minimal one.
        var compOffRequest = new CompOffRequest
        {
            Id         = Guid.NewGuid(),
            EmployeeId = user.Id,
            WorkedDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-200)),
            HoursWorked = 8,
            Reason     = "Integration day",
            Status     = CompOffStatus.Approved,
            CreatedAt  = DateTime.UtcNow.AddDays(-200),
            UpdatedAt  = DateTime.UtcNow.AddDays(-200),
        };
        db.CompOffRequests.Add(compOffRequest);

        var expiredCredit = new CompOffCredit
        {
            Id               = Guid.NewGuid(),
            EmployeeId       = user.Id,
            CompOffRequestId = compOffRequest.Id,
            CreditDays       = 1m,
            UsedDays         = 0m,
            ExpiresAt        = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), // yesterday
            CreatedAt        = DateTime.UtcNow.AddDays(-200),
        };
        var notExpiredCredit = new CompOffCredit
        {
            Id               = Guid.NewGuid(),
            EmployeeId       = user.Id,
            CompOffRequestId = compOffRequest.Id,
            CreditDays       = 1m,
            UsedDays         = 0m,
            ExpiresAt        = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)), // future
            CreatedAt        = DateTime.UtcNow.AddDays(-10),
        };
        db.CompOffCredits.AddRange(expiredCredit, notExpiredCredit);
        await db.SaveChangesAsync();

        var svc = new LeaveBalanceService(db);
        await svc.ExpireCompOffCredits();

        var expired    = await db.CompOffCredits.FindAsync(expiredCredit.Id);
        var notExpired = await db.CompOffCredits.FindAsync(notExpiredCredit.Id);

        // Expired credit: used_days must equal credit_days
        Assert.Equal(expiredCredit.CreditDays, expired!.UsedDays);
        // Not-yet-expired credit: untouched
        Assert.Equal(0m, notExpired!.UsedDays);
    }

    // ── UT-31: Balance exactly equal to requested days succeeds ───────────────

    [Fact]
    public async Task UT31_DeductBalance_ExactBalance_Succeeds()
    {
        await using var db = CreateDb();
        var user           = MakeUser();
        var lt             = MakeLeaveType(AccrualType.Annual, maxDays: 5);
        db.Users.Add(user);
        db.LeaveTypes.Add(lt);
        // allocated=5, used=0 → available=5 → request exactly 5 days
        db.LeaveBalances.Add(
            MakeBalance(user.Id, lt.Id, allocated: 5m, used: 0m,
                        year: DateTime.UtcNow.Year));
        await db.SaveChangesAsync();

        var svc    = new LeaveBalanceService(db);
        var result = await svc.DeductBalance(user.Id, lt.Id, days: 5m);

        // Must succeed — available == requested is not a shortfall
        Assert.True(result.IsSuccess);
    }
}
