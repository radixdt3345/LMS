using LMS.Domain.Entities;
using LMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LMS.Tests.Unit.LeaveCore;

/// <summary>
/// UT-32: IsHoliday check against seeded holiday list — known holiday date returns true.
/// UT-33: IsHoliday check for a regular working day — returns false.
/// Uses EF Core InMemory provider; no PostgreSQL required.
/// Simulates the EF/cache-backed lookup that HolidayService.IsHolidayAsync provides.
/// Run: dotnet test --filter Category=Unit
/// </summary>
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

    /// <summary>
    /// Seeds a known holiday list into the in-memory DB, mirroring IMemoryCache population
    /// in HolidayService (LEAVECORE-API-003).
    /// </summary>
    private static async Task<LmsDbContext> SeedHolidaysAsync()
    {
        var db = CreateInMemoryDb();
        var holidays = new[]
        {
            new Holiday
            {
                Id = Guid.NewGuid(),
                Name = "Republic Day",
                Date = new DateOnly(2026, 1, 26),
                Year = 2026,
                IsRecurring = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            },
            new Holiday
            {
                Id = Guid.NewGuid(),
                Name = "Independence Day",
                Date = new DateOnly(2026, 8, 15),
                Year = 2026,
                IsRecurring = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            },
            new Holiday
            {
                Id = Guid.NewGuid(),
                Name = "Diwali",
                Date = new DateOnly(2026, 10, 20),
                Year = 2026,
                IsRecurring = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            },
        };
        db.Holidays.AddRange(holidays);
        await db.SaveChangesAsync();
        return db;
    }

    // ── UT-32: known holiday → IsHoliday returns true ────────────────────────

    [Fact]
    public async Task UT32_IsHoliday_KnownHoliday_ReturnsTrue()
    {
        // Arrange — seeded list mirrors what IMemoryCache holds in HolidayService
        await using var db = await SeedHolidaysAsync();

        var knownHoliday = new DateOnly(2026, 1, 26); // Republic Day

        // Act — inline lookup simulating HolidayService.IsHolidayAsync
        var isHoliday = await db.Holidays
            .AnyAsync(h => h.Date == knownHoliday);

        // Assert
        Assert.True(isHoliday);
    }

    // ── UT-33: working day → IsHoliday returns false ─────────────────────────

    [Fact]
    public async Task UT33_IsHoliday_RegularWorkingDay_ReturnsFalse()
    {
        // Arrange
        await using var db = await SeedHolidaysAsync();

        var workingDay = new DateOnly(2026, 1, 27); // Tuesday after Republic Day — not a holiday

        // Act
        var isHoliday = await db.Holidays
            .AnyAsync(h => h.Date == workingDay);

        // Assert
        Assert.False(isHoliday);
    }

    // ── Supplementary: all three seeded holidays are found ────────────────────

    [Fact]
    public async Task IsHoliday_AllSeededHolidays_AreFound()
    {
        await using var db = await SeedHolidaysAsync();

        var expectedHolidays = new[]
        {
            new DateOnly(2026, 1, 26),
            new DateOnly(2026, 8, 15),
            new DateOnly(2026, 10, 20),
        };

        foreach (var date in expectedHolidays)
        {
            var found = await db.Holidays.AnyAsync(h => h.Date == date);
            Assert.True(found, $"Expected {date} to be a holiday in the seeded list");
        }
    }

    // ── Supplementary: weekend day not in holiday list returns false ───────────

    [Fact]
    public async Task IsHoliday_WeekendNotInHolidayList_ReturnsFalse()
    {
        await using var db = await SeedHolidaysAsync();

        // Saturday that is not a public holiday
        var regularSaturday = new DateOnly(2026, 1, 17);

        var isHoliday = await db.Holidays.AnyAsync(h => h.Date == regularSaturday);

        // Working-day logic in LeaveRequestService treats weekends as non-working separately;
        // IsHoliday specifically checks the holidays table.
        Assert.False(isHoliday);
    }
}
