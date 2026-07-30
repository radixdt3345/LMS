using LMS.Application.DTOs.CompOff;
using LMS.Application.Interfaces;
using LMS.Domain.Common;
using LMS.Domain.Entities;
using LMS.Domain.Enums;
using LMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LMS.Infrastructure.Services;

/// <summary>
/// Generates comp-off credits when a request is approved and updates the
/// employee's leave balance for the Comp Off leave type.
/// Conversion rules (UT-44, UT-45):
///   worked_hours >= 8 → 1.0 day credit
///   4 &lt;= worked_hours &lt; 8 → 0.5 day credit
/// Expiry: worked_date + 180 days (UT-46, COFF-002).
/// Balance update: upserts LeaveBalance for the Comp Off leave type (UT-47).
/// </summary>
public class CompOffCreditService : ICompOffCreditService
{
    private readonly LmsDbContext _db;

    public CompOffCreditService(LmsDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc/>
    public async Task<Result<CompOffCreditDto>> CreditBalanceAsync(Guid requestId)
    {
        var request = await _db.CompOffRequests.FindAsync(requestId);
        if (request is null)
            return Result<CompOffCreditDto>.Failure("Comp-off request not found.", 404);

        // UT-44: 4h → 0.5d;  UT-45: 8h → 1.0d
        var creditDays = request.WorkedHours >= 8m ? 1.0m : 0.5m;

        // UT-46: expires_at = worked_date + 180 days
        var expiresAt = request.WorkedDate.AddDays(180);

        var now = DateTime.UtcNow;
        var credit = new CompOffCredit
        {
            Id               = Guid.NewGuid(),
            EmployeeId       = request.EmployeeId,
            CompOffRequestId = request.Id,
            CreditDays       = creditDays,
            ExpiresAt        = expiresAt,
            UsedDays         = 0m,
            CreatedAt        = now,
        };
        _db.CompOffCredits.Add(credit);

        // UT-47: approved adds to comp-off leave balance
        // Identify the Comp Off leave type by AccrualType.OneTime and name substring.
        var compOffType = await _db.LeaveTypes
            .FirstOrDefaultAsync(lt =>
                lt.IsActive &&
                lt.AccrualType == AccrualType.OneTime &&
                lt.Name.Contains("Comp"));

        if (compOffType is not null)
        {
            var year    = (short)now.Year;
            var balance = await _db.LeaveBalances
                .FirstOrDefaultAsync(b =>
                    b.UserId == request.EmployeeId &&
                    b.LeaveTypeId == compOffType.Id &&
                    b.Year == year);

            if (balance is null)
            {
                _db.LeaveBalances.Add(new LeaveBalance
                {
                    Id            = Guid.NewGuid(),
                    UserId        = request.EmployeeId,
                    LeaveTypeId   = compOffType.Id,
                    Year          = year,
                    AllocatedDays = creditDays,
                    UsedDays      = 0m,
                    CreatedAt     = now,
                    UpdatedAt     = now,
                });
            }
            else
            {
                balance.AllocatedDays += creditDays;
                balance.UpdatedAt      = now;
            }
        }

        await _db.SaveChangesAsync();

        return Result<CompOffCreditDto>.Success(new CompOffCreditDto
        {
            Id               = credit.Id,
            EmployeeId       = credit.EmployeeId,
            CompOffRequestId = credit.CompOffRequestId,
            CreditDays       = credit.CreditDays,
            ExpiresAt        = credit.ExpiresAt,
            UsedDays         = credit.UsedDays,
            CreatedAt        = credit.CreatedAt,
        });
    }

    /// <inheritdoc/>
    public async Task<Result<List<CompOffCreditDto>>> GetMyCreditsAsync(Guid employeeId)
    {
        var credits = await _db.CompOffCredits
            .AsNoTracking()
            .Where(c => c.EmployeeId == employeeId)
            .OrderByDescending(c => c.ExpiresAt)
            .ToListAsync();

        return Result<List<CompOffCreditDto>>.Success(
            credits.Select(c => new CompOffCreditDto
            {
                Id               = c.Id,
                EmployeeId       = c.EmployeeId,
                CompOffRequestId = c.CompOffRequestId,
                CreditDays       = c.CreditDays,
                ExpiresAt        = c.ExpiresAt,
                UsedDays         = c.UsedDays,
                CreatedAt        = c.CreatedAt,
            }).ToList());
    }
}
