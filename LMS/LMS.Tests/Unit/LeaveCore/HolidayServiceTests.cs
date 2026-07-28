using LMS.Application.DTOs.LeaveCore;
using LMS.Domain.Entities;
using LMS.Infrastructure.Data;
using LMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LMS.Tests.Unit.LeaveCore;

/// <summary>
/// Unit tests for HolidayService.
/// UT-31: GetHolidaysAsync returns only holidays for the given year.
/// UT-32: IsWorkingDay returns false for Saturday and Sunday.
/// UT-33: IsWorkingDay returns false for a registered holiday date.
/// UT-34: CountWorkingDays counts Mon-Fri excluding holidays (inclusive range).
/// </summary>
[Trait("Category", "Unit")]
public class HolidayServiceTests
{
    // ── Helpers ───────────────────────────────────────────────────────────

    private static LmsDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<LmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new LmsDbContext(options);
    }

    private static HolidayService BuildService(LmsDbContext db) => new(db);

    private static Holiday MakeHoliday(
        string name, DateOnly date, bool isRecurring = false) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Date = date,
        Year = date.Year,
        IsRecurring = isRecurring,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    // ── UT-31: GetHolidaysAsync filters by year ───────────────────────────

    [Fact]
    public async Task UT31_GetHolidays_ReturnsOnlyHolidaysForYear()
    {
        await using var db = CreateInMemoryDb();
        db.Holidays.Add(MakeHoliday("New Year 2026", new DateOnly(2026, 1, 1)));
        db.Holidays.Add(MakeHoliday("Christmas 2025", new DateOnly(2025, 12, 25)));
        await db.SaveChangesAsync();

        var svc = BuildService(db);
        var result = await svc.GetHolidaysAsync(2026);

        Assert.True(result.IsSuccess);
        var items = result.Value!.ToList();
        Assert.Single(items);
        Assert.Equal("New Year 2026", items[0].Name);
        Assert.Equal(2026, items[0].Year);
    }

    [Fact]
    public async Task UT31b_GetHolidays_RecurringHolidayAppearsForEveryYear()
    {
        await using var db = CreateInMemoryDb();
        // Recurring holiday stored under 2025 — should appear when querying 2026.
        db.Holidays.Add(MakeHoliday("Republic Day", new DateOnly(2025, 1, 26), isRecurring: true));
        db.Holidays.Add(MakeHoliday("Budget Holiday 2025", new DateOnly(2025, 7, 1), isRecurring: false));
        await db.SaveChangesAsync();

        var svc = BuildService(db);
        var result = await svc.GetHolidaysAsync(2026);

        Assert.True(result.IsSuccess);
        var items = result.Value!.ToList();
        Assert.Single(items);
        Assert.Equal("Republic Day", items[0].Name);
    }

    // ── UT-32: IsWorkingDay returns false for weekends ────────────────────

    [Fact]
    public async Task UT32_IsWorkingDay_ReturnsFalseForSaturday()
    {
        await using var db = CreateInMemoryDb();
        var svc = BuildService(db);

        // 2026-07-25 is a Saturday
        var result = await svc.IsWorkingDayAsync(new DateOnly(2026, 7, 25));

        Assert.True(result.IsSuccess);
        Assert.False(result.Value);
    }

    [Fact]
    public async Task UT32b_IsWorkingDay_ReturnsFalseForSunday()
    {
        await using var db = CreateInMemoryDb();
        var svc = BuildService(db);

        // 2026-07-26 is a Sunday
        var result = await svc.IsWorkingDayAsync(new DateOnly(2026, 7, 26));

        Assert.True(result.IsSuccess);
        Assert.False(result.Value);
    }

    [Fact]
    public async Task UT32c_IsWorkingDay_ReturnsTrueForWeekdayWithNoHoliday()
    {
        await using var db = CreateInMemoryDb();
        var svc = BuildService(db);

        // 2026-07-27 is a Monday with no holidays seeded
        var result = await svc.IsWorkingDayAsync(new DateOnly(2026, 7, 27));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
    }

    // ── UT-33: IsWorkingDay returns false for a registered holiday ────────

    [Fact]
    public async Task UT33_IsWorkingDay_ReturnsFalseForExactHolidayDate()
    {
        await using var db = CreateInMemoryDb();
        // 2026-08-15 is Independence Day (Saturday in 2026 — use a known weekday holiday)
        // Use 2026-01-26 (Monday) as Republic Day
        var holidayDate = new DateOnly(2026, 1, 26);
        db.Holidays.Add(MakeHoliday("Republic Day", holidayDate));
        await db.SaveChangesAsync();

        var svc = BuildService(db);
        var result = await svc.IsWorkingDayAsync(holidayDate);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value); // Monday but a holiday — not a working day
    }

    [Fact]
    public async Task UT33b_IsWorkingDay_ReturnsFalseForRecurringHolidayByMonthDay()
    {
        await using var db = CreateInMemoryDb();
        // Recurring holiday stored under 2025-01-26; querying 2026-01-26 should match.
        db.Holidays.Add(MakeHoliday("Republic Day", new DateOnly(2025, 1, 26), isRecurring: true));
        await db.SaveChangesAsync();

        var svc = BuildService(db);
        // 2026-01-26 is a Monday
        var result = await svc.IsWorkingDayAsync(new DateOnly(2026, 1, 26));

        Assert.True(result.IsSuccess);
        Assert.False(result.Value); // recurring holiday matches month+day
    }

    // ── UT-34: CountWorkingDays counts Mon-Fri excluding holidays ─────────

    [Fact]
    public async Task UT34_CountWorkingDays_CountsWeekdaysExcludingHoliday()
    {
        await using var db = CreateInMemoryDb();
        // Mon 2026-07-27 to Fri 2026-07-31 = 5 weekdays
        // Subtract 1 for holiday on Wed 2026-07-29
        db.Holidays.Add(MakeHoliday("Test Holiday", new DateOnly(2026, 7, 29)));
        await db.SaveChangesAsync();

        var svc = BuildService(db);
        var result = await svc.CountWorkingDaysAsync(
            new DateOnly(2026, 7, 27),
            new DateOnly(2026, 7, 31));

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Value); // 5 weekdays - 1 holiday = 4
    }

    [Fact]
    public async Task UT34b_CountWorkingDays_ExcludesWeekendDays()
    {
        await using var db = CreateInMemoryDb();
        var svc = BuildService(db);

        // Mon 2026-07-27 to Sun 2026-08-02 = 7 days, 5 weekdays, 2 weekend days, no holidays
        var result = await svc.CountWorkingDaysAsync(
            new DateOnly(2026, 7, 27),
            new DateOnly(2026, 8, 2));

        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value);
    }

    [Fact]
    public async Task UT34c_CountWorkingDays_InvalidRange_Returns400()
    {
        await using var db = CreateInMemoryDb();
        var svc = BuildService(db);

        var result = await svc.CountWorkingDaysAsync(
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 7, 27)); // end before start

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task UT34d_CountWorkingDays_RecurringHolidayExcluded()
    {
        await using var db = CreateInMemoryDb();
        // Recurring holiday on Jan 26 — stored under 2025 but should exclude 2026-01-26
        db.Holidays.Add(MakeHoliday("Republic Day", new DateOnly(2025, 1, 26), isRecurring: true));
        await db.SaveChangesAsync();

        var svc = BuildService(db);
        // Mon 2026-01-26 to Fri 2026-01-30 = 5 weekdays, subtract 1 recurring holiday = 4
        var result = await svc.CountWorkingDaysAsync(
            new DateOnly(2026, 1, 26),
            new DateOnly(2026, 1, 30));

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Value);
    }

    // ── Extra: CreateHoliday + DeleteHoliday coverage ─────────────────────

    [Fact]
    public async Task CreateHoliday_PersistsCorrectly()
    {
        await using var db = CreateInMemoryDb();
        var svc = BuildService(db);

        var result = await svc.CreateHolidayAsync(new CreateHolidayDto
        {
            Name = "Republic Day",
            Date = new DateOnly(2026, 1, 26),
            IsRecurring = true,
        });

        Assert.True(result.IsSuccess);
        Assert.Equal("Republic Day", result.Value!.Name);
        Assert.Equal(2026, result.Value.Year);
        Assert.True(result.Value.IsRecurring);
        Assert.Equal(1, await db.Holidays.CountAsync());
    }

    [Fact]
    public async Task DeleteHoliday_UnknownId_Returns404()
    {
        await using var db = CreateInMemoryDb();
        var svc = BuildService(db);

        var result = await svc.DeleteHolidayAsync(Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task UpdateHoliday_UnknownId_Returns404()
    {
        await using var db = CreateInMemoryDb();
        var svc = BuildService(db);

        var result = await svc.UpdateHolidayAsync(
            Guid.NewGuid(),
            new UpdateHolidayDto { Name = "Ghost" });

        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.StatusCode);
    }
}
