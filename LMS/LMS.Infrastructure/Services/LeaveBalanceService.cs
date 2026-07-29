using LMS.Application.DTOs.LeaveBalance;
using LMS.Application.Interfaces;
using LMS.Domain.Common;
using LMS.Domain.Entities;
using LMS.Domain.Enums;
using LMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LMS.Infrastructure.Services;

/// <summary>
/// Implements leave balance management: allocation, deduction, restoration,
/// year-end lapse, new-joiner proration, and comp-off expiry.
/// FR-31 to FR-36. No carry-forward per POL-06.
/// </summary>
public class LeaveBalanceService : ILeaveBalanceService
{
    private readonly LmsDbContext _db;

    public LeaveBalanceService(LmsDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc/>
    public async Task<List<BalanceDto>> GetBalance(Guid userId, int year)
    {
        var balances = await _db.LeaveBalances
            .AsNoTracking()
            .Include(b => b.LeaveType)
            .Where(b => b.UserId == userId && b.Year == (short)year)
            .ToListAsync();

        return balances.Select(b => new BalanceDto
        {
            LeaveTypeId   = b.LeaveTypeId,
            LeaveTypeName = b.LeaveType.Name,
            AllocatedDays = b.AllocatedDays,
            UsedDays      = b.UsedDays,
            AvailableDays = Math.Max(0m, b.AllocatedDays - b.UsedDays),
            Year          = b.Year,
        }).ToList();
    }

    /// <inheritdoc/>
    public async Task<Result<bool>> DeductBalance(
        Guid userId, Guid leaveTypeId, decimal days)
    {
        var leaveType = await _db.LeaveTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(lt => lt.Id == leaveTypeId);

        if (leaveType is null)
            return Result<bool>.Failure("Leave type not found.", 404);

        // Unlimited accrual types (e.g. Unpaid Leave) bypass balance checks — UT-26.
        if (leaveType.AccrualType == AccrualType.Unlimited)
            return Result<bool>.Success(true);

        var year = (short)DateTime.UtcNow.Year;
        var balance = await _db.LeaveBalances
            .FirstOrDefaultAsync(b =>
                b.UserId == userId &&
                b.LeaveTypeId == leaveTypeId &&
                b.Year == year);

        if (balance is null)
            return Result<bool>.Failure("No leave balance found for this year.", 404);

        // UT-27, UT-31: reject if insufficient (available must be >= requested)
        var available = balance.AllocatedDays - balance.UsedDays;
        if (available < days)
            return Result<bool>.Failure("Insufficient leave balance.");

        balance.UsedDays  += days;
        balance.UpdatedAt  = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Result<bool>.Success(true);
    }

    /// <inheritdoc/>
    public async Task RestoreBalance(Guid userId, Guid leaveTypeId, decimal days)
    {
        var year = (short)DateTime.UtcNow.Year;
        var balance = await _db.LeaveBalances
            .FirstOrDefaultAsync(b =>
                b.UserId == userId &&
                b.LeaveTypeId == leaveTypeId &&
                b.Year == year);

        if (balance is null)
            return; // nothing to restore — no-op

        // Clamp at zero to prevent negative used_days — UT-24.
        balance.UsedDays  = Math.Max(0m, balance.UsedDays - days);
        balance.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public async Task CreditAnnual(int year)
    {
        var activeUsers = await _db.Users
            .AsNoTracking()
            .Where(u => u.IsActive)
            .Select(u => u.Id)
            .ToListAsync();

        // Only Annual and OneTime types with a configured entitlement — UT-22, UT-29.
        var leaveTypes = await _db.LeaveTypes
            .AsNoTracking()
            .Where(lt => lt.IsActive
                && lt.MaxDaysPerYear.HasValue
                && (lt.AccrualType == AccrualType.Annual
                    || lt.AccrualType == AccrualType.OneTime))
            .ToListAsync();

        var now = DateTime.UtcNow;

        foreach (var userId in activeUsers)
        {
            foreach (var lt in leaveTypes)
            {
                var existing = await _db.LeaveBalances
                    .FirstOrDefaultAsync(b =>
                        b.UserId == userId &&
                        b.LeaveTypeId == lt.Id &&
                        b.Year == (short)year);

                if (existing is null)
                {
                    _db.LeaveBalances.Add(new LeaveBalance
                    {
                        Id            = Guid.NewGuid(),
                        UserId        = userId,
                        LeaveTypeId   = lt.Id,
                        Year          = (short)year,
                        AllocatedDays = (decimal)lt.MaxDaysPerYear!.Value,
                        UsedDays      = 0m,
                        CreatedAt     = now,
                        UpdatedAt     = now,
                    });
                }
                else
                {
                    existing.AllocatedDays = (decimal)lt.MaxDaysPerYear!.Value;
                    existing.UpdatedAt     = now;
                }
            }
        }

        await _db.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public async Task ProrateForNewJoiner(Guid userId, DateTime joinDate)
    {
        var year = (short)joinDate.Year;
        var remainingMonths = 12 - joinDate.Month + 1;

        var leaveTypes = await _db.LeaveTypes
            .AsNoTracking()
            .Where(lt => lt.IsActive
                && lt.MaxDaysPerYear.HasValue
                && lt.AccrualType == AccrualType.Annual)
            .ToListAsync();

        var now = DateTime.UtcNow;

        foreach (var lt in leaveTypes)
        {
            var credit = Math.Round(
                remainingMonths / 12.0m * lt.MaxDaysPerYear!.Value,
                1,
                MidpointRounding.ToEven);

            var existing = await _db.LeaveBalances
                .FirstOrDefaultAsync(b =>
                    b.UserId == userId &&
                    b.LeaveTypeId == lt.Id &&
                    b.Year == year);

            if (existing is null)
            {
                _db.LeaveBalances.Add(new LeaveBalance
                {
                    Id            = Guid.NewGuid(),
                    UserId        = userId,
                    LeaveTypeId   = lt.Id,
                    Year          = year,
                    AllocatedDays = credit,
                    UsedDays      = 0m,
                    CreatedAt     = now,
                    UpdatedAt     = now,
                });
            }
            else
            {
                existing.AllocatedDays = credit;
                existing.UpdatedAt     = now;
            }
        }

        await _db.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public async Task YearEndLapse(int year)
    {
        // Zero all Annual and OneTime balances for the given year — POL-06, UT-28.
        var balances = await _db.LeaveBalances
            .Include(b => b.LeaveType)
            .Where(b =>
                b.Year == (short)year &&
                (b.LeaveType.AccrualType == AccrualType.Annual ||
                 b.LeaveType.AccrualType == AccrualType.OneTime))
            .ToListAsync();

        var now = DateTime.UtcNow;
        foreach (var b in balances)
        {
            b.AllocatedDays = 0m;
            b.UsedDays      = 0m;
            b.UpdatedAt     = now;
        }

        await _db.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public async Task ExpireCompOffCredits()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Credits where expiry date has passed and there are still unredeemed days — UT-30.
        var expired = await _db.CompOffCredits
            .Where(c => c.ExpiresAt <= today && c.UsedDays < c.CreditDays)
            .ToListAsync();

        foreach (var credit in expired)
            credit.UsedDays = credit.CreditDays; // mark fully consumed

        await _db.SaveChangesAsync();
    }
}
