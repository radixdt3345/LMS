using LMS.Application.DTOs.LeaveCore;
using LMS.Application.Interfaces;
using LMS.Domain.Common;
using LMS.Domain.Entities;
using LMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LMS.Infrastructure.Services;

/// <summary>
/// Implements holiday calendar management and working-day utilities.
/// FR-31 (list by year), FR-32 (create), FR-33 (update), FR-34 (delete).
/// IsWorkingDayAsync and CountWorkingDaysAsync underpin the SandwichRuleEngine.
/// </summary>
public class HolidayService : IHolidayService
{
    private readonly LmsDbContext _db;

    public HolidayService(LmsDbContext db) => _db = db;

    /// <inheritdoc/>
    public async Task<Result<IEnumerable<HolidayDto>>> GetHolidaysAsync(
        int year, CancellationToken ct = default)
    {
        // Include holidays for the exact year OR recurring holidays (appear in every year).
        var items = await _db.Holidays
            .AsNoTracking()
            .Where(h => h.Year == year || h.IsRecurring)
            .OrderBy(h => h.Date)
            .Select(h => new HolidayDto
            {
                Id = h.Id,
                Name = h.Name,
                Date = h.Date,
                Year = h.Year,
                IsRecurring = h.IsRecurring,
            })
            .ToListAsync(ct);

        return Result<IEnumerable<HolidayDto>>.Success(items);
    }

    /// <inheritdoc/>
    public async Task<Result<HolidayDto>> CreateHolidayAsync(
        CreateHolidayDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return Result<HolidayDto>.Failure("Name is required.", 400);

        var now = DateTime.UtcNow;
        var entity = new Holiday
        {
            Id = Guid.NewGuid(),
            Name = dto.Name.Trim(),
            Date = dto.Date,
            Year = dto.Date.Year,   // derived — not trusted from caller
            IsRecurring = dto.IsRecurring,
            CreatedAt = now,
            UpdatedAt = now,
        };

        _db.Holidays.Add(entity);
        await _db.SaveChangesAsync(ct);

        return Result<HolidayDto>.Success(MapToDto(entity));
    }

    /// <inheritdoc/>
    public async Task<Result<HolidayDto>> UpdateHolidayAsync(
        Guid id, UpdateHolidayDto dto, CancellationToken ct = default)
    {
        var entity = await _db.Holidays.FindAsync(new object[] { id }, ct);
        if (entity is null)
            return Result<HolidayDto>.Failure("Holiday not found.", 404);

        if (dto.Name is not null)
            entity.Name = dto.Name.Trim();

        if (dto.Date is not null)
        {
            entity.Date = dto.Date.Value;
            entity.Year = dto.Date.Value.Year;  // keep Year in sync
        }

        if (dto.IsRecurring is not null)
            entity.IsRecurring = dto.IsRecurring.Value;

        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Result<HolidayDto>.Success(MapToDto(entity));
    }

    /// <inheritdoc/>
    public async Task<Result<bool>> DeleteHolidayAsync(
        Guid id, CancellationToken ct = default)
    {
        var entity = await _db.Holidays.FindAsync(new object[] { id }, ct);
        if (entity is null)
            return Result<bool>.Failure("Holiday not found.", 404);

        _db.Holidays.Remove(entity);
        await _db.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }

    /// <inheritdoc/>
    public async Task<Result<bool>> IsWorkingDayAsync(
        DateOnly date, CancellationToken ct = default)
    {
        // Saturdays and Sundays are never working days.
        if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            return Result<bool>.Success(false);

        // Check for an exact-date holiday OR a recurring holiday matching month+day.
        var isHoliday = await _db.Holidays.AnyAsync(
            h => h.Date == date
              || (h.IsRecurring && h.Date.Month == date.Month && h.Date.Day == date.Day),
            ct);

        return Result<bool>.Success(!isHoliday);
    }

    /// <inheritdoc/>
    public async Task<Result<int>> CountWorkingDaysAsync(
        DateOnly start, DateOnly end, CancellationToken ct = default)
    {
        if (end < start)
            return Result<int>.Failure("End date must be on or after start date.", 400);

        // Load all holidays once; filter in memory to support recurring month+day matching.
        var allHolidays = await _db.Holidays.AsNoTracking().ToListAsync(ct);

        // Build sets for O(1) lookup.
        // Recurring: match by (Month, Day) on any year.
        var recurringKeys = allHolidays
            .Where(h => h.IsRecurring)
            .Select(h => (h.Date.Month, h.Date.Day))
            .ToHashSet();

        // Non-recurring: match exact DateOnly within range.
        var fixedDates = allHolidays
            .Where(h => !h.IsRecurring && h.Date >= start && h.Date <= end)
            .Select(h => h.Date)
            .ToHashSet();

        var count = 0;
        for (var d = start; d <= end; d = d.AddDays(1))
        {
            if (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                continue;
            if (fixedDates.Contains(d))
                continue;
            if (recurringKeys.Contains((d.Month, d.Day)))
                continue;
            count++;
        }

        return Result<int>.Success(count);
    }

    private static HolidayDto MapToDto(Holiday h) => new()
    {
        Id = h.Id,
        Name = h.Name,
        Date = h.Date,
        Year = h.Year,
        IsRecurring = h.IsRecurring,
    };
}
