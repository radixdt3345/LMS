using LMS.Application.DTOs.LeaveCore;
using LMS.Domain.Common;

namespace LMS.Application.Interfaces;

/// <summary>
/// Holiday calendar management.
/// Provides working-day utilities consumed by SandwichRuleEngine and LeaveRequestService.
/// FR-31 to FR-34.
/// </summary>
public interface IHolidayService
{
    /// <summary>
    /// Returns all holidays for the given year.
    /// Recurring holidays (IsRecurring=true) are included in every year query
    /// regardless of their stored Year value.
    /// </summary>
    Task<Result<IEnumerable<HolidayDto>>> GetHolidaysAsync(
        int year, CancellationToken ct = default);

    /// <summary>
    /// Creates a new holiday. Year is derived from dto.Date.Year automatically.
    /// </summary>
    Task<Result<HolidayDto>> CreateHolidayAsync(
        CreateHolidayDto dto, CancellationToken ct = default);

    /// <summary>
    /// Applies partial updates to a holiday. Returns 404 if not found.
    /// When Date is updated, Year is re-derived from the new date.
    /// </summary>
    Task<Result<HolidayDto>> UpdateHolidayAsync(
        Guid id, UpdateHolidayDto dto, CancellationToken ct = default);

    /// <summary>
    /// Hard-deletes a holiday. Returns 404 if not found.
    /// Holidays are not soft-deleted — removing a past holiday corrects historical records.
    /// </summary>
    Task<Result<bool>> DeleteHolidayAsync(
        Guid id, CancellationToken ct = default);

    /// <summary>
    /// Returns true if the given date is a Monday-Friday AND not a registered holiday.
    /// Recurring holidays match by month+day regardless of year.
    /// Used by SandwichRuleEngine (single request scope) and LeaveRequestService.
    /// </summary>
    Task<Result<bool>> IsWorkingDayAsync(
        DateOnly date, CancellationToken ct = default);

    /// <summary>
    /// Counts Monday-Friday days in [start, end] inclusive, excluding registered holidays.
    /// start must be &lt;= end; returns 400 otherwise.
    /// Used by SandwichRuleEngine to compute leave duration.
    /// </summary>
    Task<Result<int>> CountWorkingDaysAsync(
        DateOnly start, DateOnly end, CancellationToken ct = default);
}
