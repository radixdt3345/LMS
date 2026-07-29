using LMS.Application.DTOs.LeaveCore;
using LMS.Infrastructure.Data;
using LMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LMS.Tests.Integration;

/// <summary>
/// Integration tests for the holiday calendar management and working-day logic.
/// Uses a real PostgreSQL database — set TEST_DB_CONNECTION or rely on the default.
///
/// IT-19: Create a holiday, query holidays by year, verify IsWorkingDay returns false.
/// IT-20: Bulk-create 10 holidays; re-importing the same set is fully rejected
///        by the unique date+year constraint (imported = 0 on re-import).
/// </summary>
[Trait("Category", "Integration")]
public class HolidayIntegrationTests : IAsyncLifetime
{
    private DbContextOptions<LmsDbContext> _options = null!;
    private LmsDbContext _context = null!;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        var connectionString =
            Environment.GetEnvironmentVariable("TEST_DB_CONNECTION")
            ?? "Host=localhost;Database=lms_test;Username=postgres;Password=postgres";

        _options = new DbContextOptionsBuilder<LmsDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        _context = new LmsDbContext(_options);
        await _context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Creates a fresh DbContext scope for operations that need isolation.</summary>
    private LmsDbContext CreateContext() => new LmsDbContext(_options);

    // ── IT-19 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// IT-19: POST /holidays (Christmas 2026)
    ///        → GET /holidays?year=2026 contains the record
    ///        → IsWorkingDay for Dec 25 2026 returns false.
    /// </summary>
    [Fact]
    public async Task IT19_CreateChristmasHoliday_AppearsInYearQuery_AndIsNotAWorkingDay()
    {
        // Arrange
        var svc = new HolidayService(_context);
        var christmas = new DateOnly(2026, 12, 25);

        var createDto = new CreateHolidayDto
        {
            Name = "Christmas Day 2026",
            Date = christmas,
            IsRecurring = false,
        };

        // Act 1 — POST /holidays
        var createResult = await svc.CreateHolidayAsync(createDto);
        Assert.True(createResult.IsSuccess, $"CreateHoliday failed: {createResult.Error}");
        Assert.Equal(christmas, createResult.Value!.Date);
        Assert.Equal(2026, createResult.Value.Year);

        // Act 2 — GET /holidays?year=2026 → must contain Dec 25
        _context.ChangeTracker.Clear();
        var listResult = await svc.GetHolidaysAsync(2026);
        Assert.True(listResult.IsSuccess);
        var holidays = listResult.Value!.ToList();
        Assert.Contains(holidays, h => h.Date == christmas && h.Name == "Christmas Day 2026");

        // Act 3 — GET working-days for Dec 25 → false (it is a registered holiday)
        var workingDayResult = await svc.IsWorkingDayAsync(christmas);
        Assert.True(workingDayResult.IsSuccess);
        Assert.False(
            workingDayResult.Value,
            "Dec 25 2026 is a registered holiday — IsWorkingDay must be false");
    }

    // ── IT-20 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// IT-20: Bulk-import 10 holidays → imported = 10.
    ///        Re-import the same set → imported = 0 (unique date+year constraint rejects all).
    /// </summary>
    [Fact]
    public async Task IT20_BulkImport10Holidays_ReImportSameSet_NoDuplicatesCreated()
    {
        // Arrange — 10 unique dates in February 2027 (year chosen to avoid conflict with IT-19)
        const int year = 2027;
        var dtos = Enumerable.Range(1, 10).Select(day => new CreateHolidayDto
        {
            Name = $"IT-20 Holiday 2027-02-{day:D2}",
            Date = new DateOnly(year, 2, day), // Feb 1–10 2027
            IsRecurring = false,
        }).ToList();

        var svc = new HolidayService(_context);

        // Act 1 — first import: all 10 should succeed
        var firstImportCount = 0;
        foreach (var dto in dtos)
        {
            var result = await svc.CreateHolidayAsync(dto);
            if (result.IsSuccess) firstImportCount++;
        }

        Assert.Equal(10, firstImportCount);

        // Verify exactly 10 records in DB for those dates
        _context.ChangeTracker.Clear();
        var dbCount = await _context.Holidays.CountAsync(
            h => h.Year == year
              && h.Date >= new DateOnly(year, 2, 1)
              && h.Date <= new DateOnly(year, 2, 10));
        Assert.Equal(10, dbCount);

        // Act 2 — re-import same 10: each attempt must be rejected by the unique constraint.
        // Use a fresh context scope per attempt to avoid EF stale-state after an exception.
        var secondImportCount = 0;
        foreach (var dto in dtos)
        {
            await using var freshDb = CreateContext();
            var freshSvc = new HolidayService(freshDb);
            try
            {
                var result = await freshSvc.CreateHolidayAsync(dto);
                if (result.IsSuccess) secondImportCount++;
            }
            catch (DbUpdateException)
            {
                // Expected: the unique index on (date, year) rejects the duplicate.
                // imported = 0 for this entry.
            }
        }

        // Assert — no new records created on re-import
        Assert.Equal(0, secondImportCount);

        // Assert — DB count unchanged (still exactly 10)
        await using var verifyDb = CreateContext();
        var finalCount = await verifyDb.Holidays.CountAsync(
            h => h.Year == year
              && h.Date >= new DateOnly(year, 2, 1)
              && h.Date <= new DateOnly(year, 2, 10));
        Assert.Equal(10, finalCount);
    }
}
