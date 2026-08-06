using LMS.Application.DTOs.CompOff;
using LMS.Domain.Common;

namespace LMS.Application.Interfaces;

/// <summary>
/// Service contract for comp-off credit generation and retrieval.
/// Credits are created when a CompOffRequest transitions to Approved.
/// Conversion: 4 h worked = 0.5 day credit, 8 h worked = 1.0 day credit.
/// Expiry: worked_date + 180 days (set by this service, not a DB trigger).
/// UT-44, UT-45, UT-46, UT-47.
/// </summary>
public interface ICompOffCreditService
{
    /// <summary>
    /// Creates a CompOffCredit for the given approved request and updates
    /// the employee's comp-off leave balance.
    /// Returns 404 if the request does not exist.
    /// </summary>
    Task<Result<CompOffCreditDto>> CreditBalanceAsync(Guid requestId);

    /// <summary>
    /// Returns all comp-off credits for the given employee, ordered by expiry descending.
    /// </summary>
    Task<Result<List<CompOffCreditDto>>> GetMyCreditsAsync(Guid employeeId);

    /// <summary>
    /// Deducts <paramref name="days"/> from the employee's non-expired comp-off credits,
    /// consuming them FIFO by expiry date (earliest-expiring first).
    /// Called when a comp-off leave request reaches final approval.
    /// </summary>
    Task DeductCreditsAsync(Guid employeeId, decimal days);
}
