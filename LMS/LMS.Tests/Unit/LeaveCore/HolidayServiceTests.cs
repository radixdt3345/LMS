using LMS.Domain.Entities;
using LMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LMS.Tests.Unit.LeaveCore;

[Trait("Category", "Unit")]
public class HolidayServiceTests
{
    private static LmsDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<LmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new LmsDbContext(options);
    }

    private static async Task<LmsDbContext> SeedHolidaysAsync()
    {
        var db = CreateInMemoryDb();
        var holidays = new[]
        {
            new Holiday { Id = Guid.NewGuid(), Name = "Republic Day",    Date = new DateOnly(2026, 1, 26),  Year = 2026, IsRecurring = true,  CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Holiday { Id = Guid.NewGuid(), Name = "Independence Day", Date = new DateOnly(2026, 8, 15),  Year = 2026, IsRecurring = true,  CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Holiday { Id = Guid.NewGuid(), Name = "Diwali",           Date = new DateOnly(2026, 10, 20), Year = 2026, IsRecurring = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
        };
        db.Holidays.AddRange(holidays);
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task UT32_IsHoliday_KnownHoliday_ReturnsTrue()
    {
        await using var db = await SeedHolidaysAsync();
        var knownHoliday = new DateOnly(2026, 1, 26);
        var isHoliday = await db.Holidays.AnyAsync(h => h.Date == knownHoliday);
        Assert.True(isHoliday);
    }

    [Fact]
    public async Task UT33_IsHoliday_RegularWorkingDay_ReturnsFalse()
    {
        await using var db = await SeedHolidaysAsync();
        var workingDay = new DateOnly(2026, 1, 27);
        var isHoliday = await db.Holidays.AnyAsync(h => h.Date == workingDay);
        Assert.False(isHoliday);
    }

    [Fact]
    public async Task IsHoliday_AllSeededHolidays_AreFound()
    {
        await using var db = await SeedHolidaysAsync();
        var expectedHolidays = new[] { new DateOnly(2026, 1, 26), new DateOnly(2026, 8, 15), new DateOnly(2026, 10, 20) };
        foreach (var date in expectedHolidays)
        {
            var found = await db.Holidays.AnyAsync(h => h.Date == date);
            Assert.True(found, $"Expected {date} to be a holiday in the seeded list");
        }
    }

    [Fact]
    public async Task IsHoliday_WeekendNotInHolidayList_ReturnsFalse()
    {
        await using var db = await SeedHolidaysAsync();
        var regularSaturday = new DateOnly(2026, 1, 17);
        var isHoliday = await db.Holidays.AnyAsync(h => h.Date == regularSaturday);
        Assert.False(isHoliday);
    }
}
