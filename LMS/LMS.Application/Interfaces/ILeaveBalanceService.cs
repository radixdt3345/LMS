using LMS.Application.DTOs.LeaveBalance;
using LMS.Domain.Common;

namespace LMS.Application.Interfaces;

/// <summary>
/// Domain service contract for leave balance management.
/// Covers allocation, deduction, restoration, year-end lapse, and comp-off expiry.
/// FR-31 to FR-36.
/// </summary>
public interface ILeaveBalanceService
{
    /// <summary>
    /// Returns all leave type balances for <paramref name="userId"/> in the given calendar year.
    /// </summary>
    Task<List<BalanceDto>> GetBalance(Guid userId, int year);

    /// <summary>
    /// Deducts <paramref name="days"/> from the user's balance for the specified leave type.
    /// Returns <c>Result.Failure</c> if the balance is insufficient — except for
    /// <see cref="LMS.Domain.Enums.AccrualType.Unlimited"/> leave types (e.g. Unpaid Leave),
    /// which are always approved regardless of balance. UT-26.
    /// </summary>
    Task<Result<bool>> DeductBalance(Guid userId, Guid leaveTypeId, decimal days);

    /// <summary>
    /// Restores <paramref name="days"/> to the user's balance (called on leave cancellation).
    /// Reduces used_days; clamps at zero to prevent negative used_days.
    /// </summary>
    Task RestoreBalance(Guid userId, Guid leaveTypeId, decimal days);

    /// <summary>
    /// Credits all active employees for every Annual and OneTime leave type
    /// with a configured <c>MaxDaysPerYear</c>. Creates or upserts a
    /// <c>LeaveBalance</c> row per (user, leaveType, year). UT-22, UT-29.
    /// </summary>
    Task CreditAnnual(int year);

    /// <summary>
    /// Prorates the annual entitlement for a new joiner based on remaining months in the year.
    /// Formula: <c>Round((12 - joinDate.Month + 1) / 12.0 * annualEntitlement, 1, MidpointRounding.ToEven)</c>.
    /// UT-25.
    /// </summary>
    Task ProrateForNewJoiner(Guid userId, DateTime joinDate);

    /// <summary>
    /// Zeroes <c>allocated_days</c> and <c>used_days</c> for all Annual and OneTime leave type
    /// balances in the given year. No carry-forward per org policy POL-06. UT-28.
    /// </summary>
    Task YearEndLapse(int year);

    /// <summary>
    /// Marks any comp-off credits whose <c>expires_at</c> is today or earlier and still have
    /// unredeemed days as fully consumed (sets <c>used_days = credit_days</c>). UT-30.
    /// </summary>
    Task ExpireCompOffCredits();
}